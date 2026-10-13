using UnityEngine;

using Newtonsoft.Json;

public enum TelemetryRecordingState { Disabled, Recording, Degraded, Failed, Closing, Closed }
public enum TelemetryInputKind { Attack, Defense, Scan }
public enum TelemetryAttackKind { Melee, Ranged }
public enum TelemetryEnemyType { Melee = 1, Ranged = 2, Charge = 3, RhythmCharge = 4, Boss = 5, HumanBoss = 6, BeastBoss = 7 }

public readonly struct TelemetryPosition
{
    [JsonProperty("x")] public float X { get; }
    [JsonProperty("y")] public float Y { get; }
    [JsonProperty("z")] public float Z { get; }

    /// <summary>position의 월드 좌표를 Unity 객체 참조 없는 저장 값으로 복사해 반환한다.</summary>
    public TelemetryPosition(Vector3 position)
    {
        X = position.x;
        Y = position.y;
        Z = position.z;
    }
}

public readonly struct TelemetryRotation
{
    [JsonProperty("x")] public float X { get; }
    [JsonProperty("y")] public float Y { get; }
    [JsonProperty("z")] public float Z { get; }
    [JsonProperty("w")] public float W { get; }

    /// <summary>rotation의 quaternion 성분을 저장 값으로 복사해 반환한다.</summary>
    public TelemetryRotation(Quaternion rotation)
    {
        X = rotation.x;
        Y = rotation.y;
        Z = rotation.z;
        W = rotation.w;
    }
}

internal sealed class TelemetryWriteRequest
{
    public string Directory { get; }
    public string Stream { get; }
    public object Payload { get; }
    public bool IsDocument { get; }
    public bool FinalizeAttempt { get; }
    public bool IsControl { get; }
    public bool DataComplete { get; }

    /// <summary>directory·stream·payload와 문서·제어·완전성 플래그를 사용해 불변 저장 요청을 생성한다.</summary>
    public TelemetryWriteRequest(string directory, string stream, object payload, bool isDocument = false, bool finalizeAttempt = false, bool isControl = false, bool dataComplete = true)
    {
        Directory = directory;
        Stream = stream;
        Payload = payload;
        IsDocument = isDocument;
        FinalizeAttempt = finalizeAttempt;
        IsControl = isControl;
        DataComplete = dataComplete;
    }
}
