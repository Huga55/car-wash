using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum WashingTool
{
    Hose,
    Foam,
    Sponge,
    Dryer
}

/// <summary>Tool presentation data. Adding a tool never changes the wash logic.</summary>
public static class WashingTools
{
    public static WashingTool ForStage(WashStage stage)
    {
        switch (stage)
        {
            case WashStage.Foam: return WashingTool.Foam;
            case WashStage.Scrub: return WashingTool.Sponge;
            case WashStage.Dry: return WashingTool.Dryer;
            default: return WashingTool.Hose;
        }
    }

    public static string Label(WashStage stage)
    {
        return Loc.StageTool(stage);
    }

    public static Sprite Icon(WashStage stage)
    {
        return UiFactory.ToolSprite(ForStage(stage));
    }

    /// <summary>Default is 25% larger than the original brush. The multiplier
    /// is exposed on GameController so the balance can be tuned in Inspector
    /// without touching code.</summary>
    public static float Radius(WashStage stage, float carScale, float multiplier = 1.25f)
    {
        float baseRadius;
        switch (stage)
        {
            case WashStage.Scrub: baseRadius = 0.070f; break;
            case WashStage.Dry: baseRadius = 0.100f; break;
            case WashStage.Foam: baseRadius = 0.095f; break;
            default: baseRadius = carScale < 0.80f ? 0.098f : 0.086f; break;
        }
        // Small cars occupy less of the wash rectangle; scale the brush with
        // the visible body instead of using one UV radius for every vehicle.
        float sizeFactor = Mathf.Lerp(0.90f, 1.08f,
            Mathf.Clamp01((carScale - 0.74f) / 0.26f));
        return baseRadius * Mathf.Clamp(multiplier, 0.8f, 1.6f) * sizeFactor;
    }

    public static string Sound(WashStage stage)
    {
        switch (stage)
        {
            case WashStage.Foam: return "foam";
            case WashStage.Rinse: return "rinse";
            case WashStage.Scrub: return "scrub";
            case WashStage.Dry: return "dry";
            default: return "splash";
        }
    }
}

/// <summary>One input layer for mouse and touch. It interpolates between frames
/// and reuses collections so dragging never allocates per-frame garbage.</summary>
public sealed class WashingController
{
    private readonly RectTransform _washRect;
    private RectTransform _carRect;
    private float _radiusMultiplier = 1.25f;
    private readonly HashSet<int> _seen = new HashSet<int>();
    private readonly List<int> _stale = new List<int>();
    private readonly Dictionary<int, Vector2> _last = new Dictionary<int, Vector2>();
    private WashSurface _surface;
    private float _radius;

    public Action<WashStage> StrokeStarted;
    public Action<Vector2, WashStage> StrokeMoved;
    public Action StrokeEnded;
    private bool _wasActive;

    public WashingController(RectTransform washRect)
    {
        _washRect = washRect;
    }

    public void Begin(WashSurface surface, float carScale, RectTransform carRect, float radiusMultiplier)
    {
        _surface = surface;
        _carRect = carRect != null ? carRect : _washRect;
        _radiusMultiplier = radiusMultiplier;
        _radius = 0.09f;
        _last.Clear();
        _seen.Clear();
        _stale.Clear();
        _wasActive = false;
        SetCarScale(carScale);
    }

    public void SetCarScale(float carScale)
    {
        // Recomputed on stage changes so the sponge is smaller than the hose.
        _carScale = carScale;
    }

    private float _carScale;

    public void Update(WashStage stage, bool active)
    {
        _radius = WashingTools.Radius(stage, _carScale, _radiusMultiplier);
        _seen.Clear();

        if (!active || _surface == null)
        {
            _last.Clear();
            return;
        }

        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) continue;
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId)) continue;
                AddPointer(touch.fingerId, touch.position, stage);
            }
        }
        else if (Input.GetMouseButton(0))
        {
            if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
                AddPointer(-1, Input.mousePosition, stage);
        }

        _stale.Clear();
        foreach (int id in _last.Keys)
            if (!_seen.Contains(id)) _stale.Add(id);
        for (int i = 0; i < _stale.Count; i++) _last.Remove(_stale[i]);

        bool hasPointers = _last.Count > 0;
        if (_wasActive && !hasPointers && StrokeEnded != null) StrokeEnded();
        _wasActive = hasPointers;
    }

    public float Progress(WashStage stage)
    {
        return _surface == null ? 0f : _surface.Progress(stage);
    }

    public float MinZoneProgress(WashStage stage)
    {
        return _surface == null ? 0f : _surface.MinZoneProgress(stage);
    }

    public float ZoneProgress(WashStage stage, int zone)
    {
        return _surface == null ? 0f : _surface.ZoneProgress(stage, zone);
    }

    public bool HasZone(int zone)
    {
        return _surface != null && _surface.HasZone(zone);
    }

    public bool BeginCompletion(WashStage stage)
    {
        _last.Clear();
        return _surface != null && _surface.BeginCompletion(stage);
    }

    public float AdvanceCompletion(float t)
    {
        return _surface == null ? 1f : _surface.AdvanceCompletion(t);
    }

    public void FillCurrent(WashStage stage)
    {
        _last.Clear();
        if (_surface != null) _surface.FillCurrent(stage);
    }

    public void BeginFinalShine()
    {
        _last.Clear();
        if (_surface != null) _surface.BeginFinalShine();
    }

    public void FinishAll()
    {
        _last.Clear();
        if (_surface != null) _surface.FinishAll();
    }

    private void AddPointer(int id, Vector2 screenPosition, WashStage stage)
    {
        _seen.Add(id);
        Vector2 uv;
        if (!TryScreenToUv(screenPosition, out uv))
        {
            _last.Remove(id);
            return;
        }

        Vector2 previous;
        if (_last.TryGetValue(id, out previous))
        {
            _surface.PaintLine(previous.x, previous.y, uv.x, uv.y, _radius, stage);
        }
        else
        {
            _surface.Paint(uv.x, uv.y, _radius, stage);
            _surface.Upload();
            if (StrokeStarted != null) StrokeStarted(stage);
        }
        if (StrokeMoved != null) StrokeMoved(uv, stage);
        _last[id] = uv;
    }

    private bool TryScreenToUv(Vector2 screenPosition, out Vector2 uv)
    {
        uv = Vector2.zero;
        Vector2 local;
        RectTransform target = _carRect != null ? _carRect : _washRect;
        // Hit-test against the moving car image, not the static wash frame.
        // The image bobs and turns a little while idle; using the frame here
        // caused the finger and the painted pixels to diverge at the edges.
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(target, screenPosition, null, out local))
            return false;

        Rect r = target.rect;
        float u = local.x / Mathf.Max(1f, r.width) + 0.5f;
        float v = local.y / Mathf.Max(1f, r.height) + 0.5f;
        if (u < 0f || u > 1f || v < 0f || v > 1f) return false;
        if (_surface != null && !_surface.IsPaintable(u, v)) return false;
        uv = new Vector2(u, v);
        return true;
    }
}
