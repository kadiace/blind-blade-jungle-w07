using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

using Newtonsoft.Json;

internal sealed class TelemetryFileWriter
{
    private const int MAX_OPEN_ATTEMPTS = 3;
    private const int RESERVED_FINALIZATIONS = 16;
    private static readonly UTF8Encoding _utf8 = new UTF8Encoding(false);

    private readonly object _gate = new object();
    private readonly Queue<TelemetryWriteRequest> _queue = new Queue<TelemetryWriteRequest>();
    private readonly Dictionary<string, OutputStream> _streams = new Dictionary<string, OutputStream>();
    private readonly Dictionary<string, Dictionary<string, long>> _written = new Dictionary<string, Dictionary<string, long>>();
    private readonly AutoResetEvent _wake = new AutoResetEvent(false);
    private readonly Thread _thread;
    private readonly string _root;
    private readonly int _capacity;
    private readonly int _reserved;
    private readonly long _fileSize;
    private readonly int _flushInterval;
    private bool _stopping;
    private volatile bool _completed;
    private volatile string _error;

    public bool Completed => _completed;
    public string Error => _error;

    /// <summary>root와 큐·파일·flush 제한값으로 저장 작업자를 시작하고 출력 파일 상태를 소유한다.</summary>
    public TelemetryFileWriter(string root, int capacity, int reserved, long fileSize, int flushInterval)
    {
        _root = root;
        _capacity = capacity;
        _reserved = reserved;
        _fileSize = fileSize;
        _flushInterval = flushInterval;
        _thread = new Thread(Run) { IsBackground = true, Name = "Play telemetry writer" };
        _thread.Start();
    }

    /// <summary>request를 critical 예약 용량 규칙에 따라 큐에 추가하고 수락 여부를 반환한다.</summary>
    public bool TryEnqueue(TelemetryWriteRequest request, bool critical)
    {
        lock (_gate)
        {
            int limit = request.FinalizeAttempt || request.IsControl ? _capacity + RESERVED_FINALIZATIONS : critical ? _capacity : _capacity - _reserved;
            if (_stopping || _error != null || _queue.Count >= limit) return false;
            _queue.Enqueue(request);
            _wake.Set();
        }
        return true;
    }

    /// <summary>추가 요청을 차단하고 기존 큐를 비운 뒤 작업자가 파일을 닫도록 요청한다.</summary>
    public void Stop()
    {
        lock (_gate)
        {
            _stopping = true;
            if (!_completed) _wake.Set();
        }
    }

    /// <summary>timeoutMilliseconds 동안 작업자 종료를 기다리고 종료 완료 여부를 반환한다.</summary>
    public bool WaitForCompletion(int timeoutMilliseconds)
    {
        return _thread.Join(timeoutMilliseconds);
    }

    /// <summary>순서대로 저장 요청을 처리하고 주기적으로 flush하며 오류와 최종 종료 상태를 공개한다.</summary>
    private void Run()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            Directory.CreateDirectory(_root);
            while (true)
            {
                TelemetryWriteRequest request = null;
                lock (_gate)
                {
                    if (_queue.Count > 0) request = _queue.Dequeue();
                    else if (_stopping) break;
                }
                if (request != null) Write(request);
                if (clock.ElapsedMilliseconds >= _flushInterval)
                {
                    FlushStreams();
                    clock.Restart();
                }
                if (request == null) _wake.WaitOne(_flushInterval);
            }
        }
        catch (Exception exception)
        {
            _error = exception.GetType().Name + ": " + exception.Message;
        }
        finally
        {
            foreach (OutputStream stream in _streams.Values)
            {
                try { stream.Dispose(); }
                catch (Exception exception) { _error = exception.GetType().Name + ": " + exception.Message; }
            }
            _streams.Clear();
            lock (_gate)
            {
                _queue.Clear();
                _wake.Dispose();
                _completed = true;
            }
        }
    }

    /// <summary>request의 순수 데이터를 JSON으로 저장하며 시도 종료 요청에는 저장 완료 건수를 포함한다.</summary>
    private void Write(TelemetryWriteRequest request)
    {
        string directory = Path.Combine(_root, request.Directory);
        Directory.CreateDirectory(directory);
        object payload = request.Payload;
        if (request.FinalizeAttempt)
        {
            FlushStreams();
            _written.TryGetValue(request.Directory, out Dictionary<string, long> counts);
            payload = new { collected = payload, written_records = counts ?? new Dictionary<string, long>(),
                storage_completed = true, data_complete = request.DataComplete };
            var closed = new List<string>();
            foreach (KeyValuePair<string, OutputStream> pair in _streams)
            {
                if (Path.GetDirectoryName(pair.Key) != request.Directory) continue;
                pair.Value.Dispose();
                closed.Add(pair.Key);
            }
            foreach (string key in closed) _streams.Remove(key);
        }
        string json = JsonConvert.SerializeObject(payload, new JsonSerializerSettings { Culture = CultureInfo.InvariantCulture });
        if (request.IsDocument)
        {
            using (FileStream file = OpenNewFile(Path.Combine(directory, request.Stream + ".json")))
            using (var writer = new StreamWriter(file, _utf8)) writer.Write(json);
        }
        else
        {
            string key = Path.Combine(request.Directory, request.Stream);
            if (!_streams.TryGetValue(key, out OutputStream output))
            {
                output = new OutputStream(directory, request.Stream);
                _streams.Add(key, output);
            }
            output.Write(json, _fileSize);
            if (!_written.TryGetValue(request.Directory, out Dictionary<string, long> counts))
            {
                counts = new Dictionary<string, long>();
                _written.Add(request.Directory, counts);
            }
            counts.TryGetValue(request.Stream, out long count);
            counts[request.Stream] = count + 1;
        }
        if (!request.FinalizeAttempt) return;
        _written.Remove(request.Directory);
    }

    /// <summary>모든 열린 스트림의 버퍼를 배출하여 기록된 데이터를 운영체제에 전달한다.</summary>
    private void FlushStreams()
    {
        foreach (OutputStream stream in _streams.Values) stream.Flush();
    }

    /// <summary>path에 새 파일을 생성하며 일시적인 생성 오류만 제한적으로 재시도해 스트림을 반환한다.</summary>
    private static FileStream OpenNewFile(string path)
    {
        for (int attempt = 0; ; attempt++)
        {
            try { return new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 65536); }
            catch (IOException) when (attempt + 1 < MAX_OPEN_ATTEMPTS && !File.Exists(path)) { Thread.Sleep(50 * (attempt + 1)); }
        }
    }

    private sealed class OutputStream : IDisposable
    {
        private readonly string _directory;
        private readonly string _name;
        private StreamWriter _writer;
        private long _bytes;
        private int _segment;

        /// <summary>directory와 name으로 파일 분할 스트림을 생성하고 파일 열기는 첫 쓰기까지 지연한다.</summary>
        public OutputStream(string directory, string name)
        {
            _directory = directory;
            _name = name;
        }

        /// <summary>json 한 줄을 maxBytes 기준으로 분할 저장하고 현재 파일의 기록 크기를 갱신한다.</summary>
        public void Write(string json, long maxBytes)
        {
            int bytes = _utf8.GetByteCount(json) + _utf8.GetByteCount(Environment.NewLine);
            if (_writer == null || (_bytes > 0 && _bytes + bytes > maxBytes))
            {
                _writer?.Dispose();
                _segment++;
                _writer = new StreamWriter(OpenNewFile(Path.Combine(_directory, _name + "_" + _segment.ToString("D4", CultureInfo.InvariantCulture) + ".jsonl")), _utf8);
                _bytes = 0;
            }
            _writer.WriteLine(json);
            _bytes += bytes;
        }

        /// <summary>현재 파일의 쓰기 버퍼를 배출하며 파일을 닫지 않는다.</summary>
        public void Flush() { _writer?.Flush(); }

        /// <summary>현재 파일을 닫고 작업자 소유의 스트림 참조를 해제한다.</summary>
        public void Dispose() { _writer?.Dispose(); _writer = null; }
    }
}
