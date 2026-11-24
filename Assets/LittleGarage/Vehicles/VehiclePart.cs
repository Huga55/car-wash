using UnityEngine;

namespace LittleGarage
{
    public sealed class VehiclePart : MonoBehaviour
    {
        [SerializeField] private PartKind kind;
        [SerializeField] private bool broken;
        [SerializeField] private bool installed = true;
        [SerializeField] private GameObject goodVisual;
        [SerializeField] private GameObject damagedVisual;
        [SerializeField] private int fastenersRemaining;
        [SerializeField] private int fastenersTotal = 4;

        private Vector3 _installedLocalPos;
        private Quaternion _installedLocalRot;
        private Vector3 _installedLocalScale;
        private Transform _installedParent;
        private bool _cachedPose;
        private Vector3 _detachTargetLocalPos;
        private Quaternion _detachTargetLocalRot;
        private bool _hasDetachTarget;
        private float _detachT;
        private float _attachT;
        private bool _detaching;
        private bool _attaching;

        public PartKind Kind
        {
            get { return kind; }
            set { kind = value; }
        }

        public bool Broken
        {
            get { return broken; }
            set { SetBroken(value); }
        }

        public bool Installed
        {
            get { return installed; }
            set { SetInstalled(value); }
        }

        public GameObject GoodVisual
        {
            get { return goodVisual; }
            set { goodVisual = value; SyncVisuals(); }
        }

        public GameObject DamagedVisual
        {
            get { return damagedVisual; }
            set { damagedVisual = value; SyncVisuals(); }
        }

        public int FastenersRemaining
        {
            get { return fastenersRemaining; }
            set { fastenersRemaining = Mathf.Clamp(value, 0, Mathf.Max(0, fastenersTotal)); }
        }

        public int FastenersTotal
        {
            get { return fastenersTotal; }
            set { fastenersTotal = Mathf.Max(0, value); fastenersRemaining = Mathf.Clamp(fastenersRemaining, 0, fastenersTotal); }
        }

        public bool IsAnimating { get { return _detaching || _attaching; } }

        private void Awake()
        {
            CachePose();
            if (fastenersTotal <= 0) fastenersTotal = GameTypes.IsWheel(kind) ? 4 : 2;
            fastenersRemaining = Mathf.Clamp(fastenersRemaining, 0, fastenersTotal);
            SyncVisuals();
        }

        public void Configure(PartKind partKind, GameObject good, GameObject damaged, int fasteners = -1)
        {
            kind = partKind;
            goodVisual = good;
            damagedVisual = damaged;
            if (fasteners >= 0) fastenersTotal = fasteners;
            if (fastenersTotal <= 0) fastenersTotal = GameTypes.IsWheel(kind) ? 4 : 2;
            fastenersRemaining = fastenersTotal;
            broken = false;
            installed = true;
            CachePose();
            SyncVisuals();
        }

        public void CachePose()
        {
            _installedParent = transform.parent;
            _installedLocalPos = transform.localPosition;
            _installedLocalRot = transform.localRotation;
            _installedLocalScale = transform.localScale;
            _cachedPose = true;
        }

        public void SetBroken(bool v)
        {
            broken = v;
            SyncVisuals();
        }

        public void SetInstalled(bool v)
        {
            installed = v;
            if (installed)
            {
                if (_cachedPose)
                {
                    transform.localPosition = _installedLocalPos;
                    transform.localRotation = _installedLocalRot;
                    transform.localScale = _installedLocalScale;
                }
                gameObject.SetActive(true);
            }
            SyncVisuals();
        }

        public bool StepFastener()
        {
            if (fastenersRemaining <= 0) return true;
            fastenersRemaining--;
            return fastenersRemaining <= 0;
        }

        public void ResetFasteners()
        {
            fastenersRemaining = Mathf.Max(0, fastenersTotal);
        }

        public void Detach(Vector3 localOffset, Vector3 localEulerOffset, float duration = 0.35f)
        {
            if (!_cachedPose) CachePose();
            _detachTargetLocalPos = _installedLocalPos + localOffset;
            _detachTargetLocalRot = Quaternion.Euler(localEulerOffset.x, localEulerOffset.y, localEulerOffset.z) * _installedLocalRot;
            _hasDetachTarget = true;
            _detachT = 0f;
            _attachT = 0f;
            _detaching = true;
            _attaching = false;
            installed = false;
            if (duration <= 0f)
            {
                transform.localPosition = _detachTargetLocalPos;
                transform.localRotation = _detachTargetLocalRot;
                _detaching = false;
            }
        }

        public void Attach(float duration = 0.35f)
        {
            if (!_cachedPose) CachePose();
            gameObject.SetActive(true);
            _attachT = 0f;
            _detachT = 0f;
            _attaching = true;
            _detaching = false;
            installed = true;
            if (duration <= 0f)
            {
                transform.localPosition = _installedLocalPos;
                transform.localRotation = _installedLocalRot;
                transform.localScale = _installedLocalScale;
                _attaching = false;
                SyncVisuals();
            }
        }

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            if (_detaching)
            {
                _detachT += dt / 0.35f;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_detachT));
                Vector3 from = _installedLocalPos;
                Quaternion fromR = _installedLocalRot;
                Vector3 to = _hasDetachTarget ? _detachTargetLocalPos : _installedLocalPos + Vector3.up * 0.2f;
                Quaternion toR = _hasDetachTarget ? _detachTargetLocalRot : _installedLocalRot;
                transform.localPosition = Vector3.LerpUnclamped(from, to, t);
                transform.localRotation = Quaternion.SlerpUnclamped(fromR, toR, t);
                if (_detachT >= 1f)
                {
                    _detaching = false;
                    transform.localPosition = to;
                    transform.localRotation = toR;
                }
            }
            else if (_attaching)
            {
                _attachT += dt / 0.35f;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_attachT));
                Vector3 from = transform.localPosition;
                Quaternion fromR = transform.localRotation;
                if (_attachT <= dt / 0.35f + 0.0001f)
                {
                    from = _hasDetachTarget ? _detachTargetLocalPos : _installedLocalPos + Vector3.up * 0.15f;
                    fromR = _hasDetachTarget ? _detachTargetLocalRot : _installedLocalRot;
                }
                transform.localPosition = Vector3.LerpUnclamped(from, _installedLocalPos, t);
                transform.localRotation = Quaternion.SlerpUnclamped(fromR, _installedLocalRot, t);
                if (_attachT >= 1f)
                {
                    _attaching = false;
                    transform.localPosition = _installedLocalPos;
                    transform.localRotation = _installedLocalRot;
                    transform.localScale = _installedLocalScale;
                }
            }
        }

        public void SetHighlight(bool on)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                Material mat = r.sharedMaterial;
                if (mat == null) continue;
                if (mat.HasProperty("_EmissionColor"))
                {
                    if (on)
                    {
                        mat.EnableKeyword("_EMISSION");
                        Color baseCol = mat.HasProperty("_Color") ? mat.color : Color.white;
                        mat.SetColor("_EmissionColor", baseCol * 0.35f);
                    }
                    else
                    {
                        mat.SetColor("_EmissionColor", Color.black);
                    }
                }
            }
        }

        private void SyncVisuals()
        {
            if (goodVisual != null) goodVisual.SetActive(!broken);
            if (damagedVisual != null) damagedVisual.SetActive(broken);
            if (goodVisual == null && damagedVisual == null)
            {
                Renderer[] rs = GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < rs.Length; i++)
                {
                    if (rs[i] != null) rs[i].enabled = installed || _detaching || _attaching;
                }
            }
            if (!installed && !_detaching && !_attaching)
            {
                if (goodVisual != null) goodVisual.SetActive(false);
                if (damagedVisual != null) damagedVisual.SetActive(false);
            }
        }
    }
}
