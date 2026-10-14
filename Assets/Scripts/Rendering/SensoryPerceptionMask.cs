using UnityEngine;

/// <summary>
    /// 관측자와 수신한 소리 위치로 윤곽선의 공간·시간 인지 영역을 셰이더에 전달한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SensoryManager))]
    public sealed class SensoryPerceptionMask : MonoBehaviour
    {
        private const int MAX_SOUND_REGIONS = 64;
        private static readonly int _soundRegionsId = Shader.PropertyToID("_OutlineSoundRegions");
        private static readonly int _soundRegionRadiiId = Shader.PropertyToID("_OutlineSoundRegionRadii");
        private static readonly int _soundCountId = Shader.PropertyToID("_OutlineSoundCount");
        private static readonly int _edgeFadeId = Shader.PropertyToID("_OutlineEdgeFade");
        private static readonly int _perceptionTimeId = Shader.PropertyToID("_OutlinePerceptionTime");

        [Header("소리 주변 인지")]
        [SerializeField, Min(0f), Tooltip("일반 소리 볼륨 1의 인지 반경(미터). 스캔은 별도의 Wave 반경을 사용한다.")]
        private float _soundRadius = 4f;
        [SerializeField, Min(0.01f), Tooltip("소리 인지 유지 시간(초). 스캔은 확산 종료 후 이 시간만큼 유지한다.")]
        private float _soundLifetime = 5f;
        private readonly Vector4[] _soundRegions = new Vector4[MAX_SOUND_REGIONS];
        private readonly Vector4[] _soundRegionRadii = new Vector4[MAX_SOUND_REGIONS];
        private int _soundCount;
        private bool _footstepOnly;

        [Header("인지 경계")]
        [SerializeField, Min(0.01f), Tooltip("인지 반경 안쪽에서 가장자리까지 흐려지는 거리(미터).")]
        private float _edgeFade = 0.75f;
        private SensoryManager _sensoryManager;

        void Awake()
        {
            _sensoryManager = GetComponent<SensoryManager>();
        }

        void OnEnable()
        {
            _sensoryManager.PerceptionRequested += HandlePerceptionRequested;
        }

        void OnDisable()
        {
            _sensoryManager.PerceptionRequested -= HandlePerceptionRequested;
            ClearMask();
        }

        void LateUpdate()
        {
            PublishMask();
        }

        /// <summary>radius(미터)로 소리 인지 반경을 변경하고 이전 반경을 반환한다. 이전 인지 영역은 지운다.</summary>
        public float SetSoundRadius(float radius)
        {
            float previous = _soundRadius;
            _soundRadius = Mathf.Max(0f, radius);
            ClearMask();
            PublishMask();
            return previous;
        }

        /// <summary>enabled로 착지 전용 표시를 전환하고 이전 영역을 비운다.</summary>
        public void SetFootstepOnly(bool enabled)
        {
            _footstepOnly = enabled;
            ClearMask();
            PublishMask();
        }

        /// <summary>착지 position 주변을 기존 파동 속도로 공개하고 확산 종료 후 _soundLifetime 동안 유지한다.</summary>
        public void RevealFootstep(Vector3 position)
        {
            if (_sensoryManager.IsReady)
                RegisterRegion(position);
        }

        /// <summary>
        /// position 주변 임시 시야를 지정 radius까지 확장하며 공통 파동 반경은 변경하지 않는다.
        /// radius는 미터 단위의 최대 반경이며 0 이하이면 새 영역을 등록하지 않는다.
        /// </summary>
        public void RevealFootstep(Vector3 position, float radius)
        {
            if (_sensoryManager.IsReady && radius > 0f)
                RegisterRegion(position, radius);
        }

        /// <summary>
        /// position의 현재 인지 가시성을 셰이더와 같은 소리 반경·확산·수명 규칙으로 반환한다.
        /// 지형 기억은 포함하지 않으며 인지 매니저가 준비되지 않으면 0을 반환한다.
        /// </summary>
        public float GetVisibility(Vector3 position)
        {
            if (!isActiveAndEnabled || !_sensoryManager.IsReady)
                return 0f;

            float visibility = 0f;
            for (int i = 0; i < _soundCount; i++)
            {
                Vector4 region = _soundRegions[i];
                float age = Mathf.Max(0f, Time.time - region.w);
                Vector4 regionRadii = _soundRegionRadii[i];
                float expansion = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / Mathf.Max(regionRadii.z, 0.0001f)));
                float radius = Mathf.Lerp(regionRadii.x, regionRadii.y, expansion);
                float fade = age < regionRadii.z + regionRadii.w ? 1f : 0f;
                visibility = Mathf.Max(visibility,
                    RegionVisibility(position, new Vector3(region.x, region.y, region.z), radius) * fade);
            }
            return visibility;
        }

        /// <summary>
        /// position과 center의 3D 거리로 radius 안쪽의 가시성을 반환한다.
        /// 인지 경계의 _edgeFade 구간에서 셰이더와 동일하게 부드럽게 감소한다.
        /// </summary>
        private float RegionVisibility(Vector3 position, Vector3 center, float radius)
        {
            if (radius <= 0f)
                return 0f;
            float innerRadius = Mathf.Max(0f, radius - Mathf.Max(_edgeFade, 0.0001f));
            float progress = Mathf.InverseLerp(innerRadius, radius, Vector3.Distance(position, center));
            return 1f - Mathf.SmoothStep(0f, 1f, progress);
        }

        /// <summary>
        /// 사건의 월드 위치와 발생 시간을 버퍼에 등록하고 가장 오래된 영역부터 교체한다.
        /// </summary>
        private void HandlePerceptionRequested(SensoryEvent sensoryEvent)
        {
            if (_footstepOnly)
                return;
            RegisterRegion(sensoryEvent.Position);
        }

        /// <summary>
        /// position과 volume으로 즉시 소리 인지 영역을 등록한다.
        /// 반경은 볼륨에 비례하며 각 영역은 독립적으로 _soundLifetime 동안 유지된다.
        /// </summary>
        public void RevealSound(Vector3 position, float volume)
        {
            if (_sensoryManager.IsReady && volume > 0f)
                RegisterRegion(position, _soundRadius * volume, false);
        }

        /// <summary>
        /// position에 radius의 임시 영역을 등록하며 expanding이면 기존 파동 속도로 확산한다.
        /// 가장 오래된 임시 표시 영역은 버퍼가 가득 차면 교체한다.
        /// </summary>
        private void RegisterRegion(Vector3 position, float radius = -1f, bool expanding = true)
        {
            PublishMask();
            if (_soundCount == MAX_SOUND_REGIONS)
            {
                for (int i = 1; i < _soundCount; i++)
                {
                    _soundRegions[i - 1] = _soundRegions[i];
                    _soundRegionRadii[i - 1] = _soundRegionRadii[i];
                }
                _soundCount--;
            }

            float startRadius = _sensoryManager.WaveStartRadius;
            float endRadius = _sensoryManager.WaveEndRadius;
            if (radius >= 0f)
            {
                endRadius = radius;
                startRadius = Mathf.Min(startRadius, endRadius);
            }
            float expansionDuration = expanding ? _sensoryManager.WaveDuration : 0f;
            _soundRegionRadii[_soundCount] = new Vector4(expanding ? startRadius : endRadius,
                endRadius, expansionDuration, _soundLifetime);
            _soundRegions[_soundCount++] = new Vector4(position.x, position.y, position.z, Time.time);
            PublishMask();
        }

        /// <summary>
        /// 만료된 사건을 제거하고 현재 관측자와 설정값 및 유효 사건을 셰이더에 전달한다.
        /// </summary>
        private void PublishMask()
        {
            if (!_sensoryManager.IsReady)
            {
                ClearMask();
                return;
            }

            float currentTime = Time.time;
            int remainingCount = 0;
            for (int i = 0; i < _soundCount; i++)
            {
                if (currentTime - _soundRegions[i].w < _soundRegionRadii[i].z + _soundRegionRadii[i].w)
                {
                    _soundRegionRadii[remainingCount] = _soundRegionRadii[i];
                    _soundRegions[remainingCount++] = _soundRegions[i];
                }
            }
            _soundCount = remainingCount;

            Shader.SetGlobalFloat(_edgeFadeId, _edgeFade);
            Shader.SetGlobalFloat(_perceptionTimeId, currentTime);
            Shader.SetGlobalInt(_soundCountId, _soundCount);
            if (_soundCount > 0)
            {
                Shader.SetGlobalVectorArray(_soundRegionsId, _soundRegions);
                Shader.SetGlobalVectorArray(_soundRegionRadiiId, _soundRegionRadii);
            }
        }

        /// <summary>
        /// 관측자 미연결 또는 세션 비활성 상태에서 모든 인지 영역을 숨기고 사건 버퍼를 비운다.
        /// </summary>
        private void ClearMask()
        {
            _soundCount = 0;
            Shader.SetGlobalInt(_soundCountId, 0);
        }
    }
