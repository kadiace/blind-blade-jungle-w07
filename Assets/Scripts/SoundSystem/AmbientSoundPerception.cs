using UnityEngine;

using FMODUnity;

/// <summary>환경 오브젝트의 기존 FMOD 반복음을 위치 기반 음량과 주기적인 공간 인지에 연결한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(StudioEventEmitter))]
public sealed class AmbientSoundPerception : MonoBehaviour
{
    [Header("환경 소리")]
    [SerializeField, Range(0f, 1f)] private float _volume = 0.25f;
    [SerializeField, Min(0.1f)] private float _refreshInterval = 3f;
    [SerializeField, Min(0.1f)] private float _audibleDistance = 15f;
    private StudioEventEmitter _emitter;
    private float _nextRevealTime;

    void Awake()
    {
        _emitter = GetComponent<StudioEventEmitter>();
    }

    void OnEnable()
    {
        _nextRevealTime = 0f;
    }

    void Update()
    {
        if (!Game.IsInitialized) return;
        SensoryManager sensory = Game.Instance.SensoryManager;
        if (!sensory.IsReady || Time.timeScale <= 0f) return;
        Vector3 offset = transform.position - sensory.Observer.position;
        float distance = offset.magnitude;
        if (distance >= _audibleDistance || _volume <= 0f)
        {
            if (_emitter.IsActive) _emitter.Stop();
            return;
        }
        if (!_emitter.IsActive) _emitter.Play();
        if (!_emitter.IsPlaying()) return;
        _emitter.EventInstance.setVolume(_volume * (1f - distance / _audibleDistance));
        if (_emitter.EventInstance.getChannelGroup(out FMOD.ChannelGroup group) == FMOD.RESULT.OK)
            group.setPan(Mathf.Clamp(Vector3.Dot(offset.normalized, sensory.Observer.right), -1f, 1f));
        if (Time.time < _nextRevealTime) return;
        _nextRevealTime = Time.time + _refreshInterval;
        sensory.ReportSound(transform.position, _volume, gameObject);
    }

    void OnDisable()
    {
        _emitter.Stop();
    }
}
