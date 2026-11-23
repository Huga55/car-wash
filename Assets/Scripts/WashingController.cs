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

    public static float Radius(WashStage stage, float carScale)
    {
        switch (stage)
        {
            case WashStage.Scrub: return 0.070f;
            case WashStage.Dry: return 0.100f;
            case WashStage.Foam: return 0.095f;
            default: return carScale < 0.80f ? 0.098f : 0.086f;
        }
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
    private readonly HashSet<int> _seen = new HashSet<int>();
    private readonly List<int> _stale = new List<int>();
    private readonly Dictionary<int, Vector2> _last = new Dictionary<int, Vector2>();
    private WashSurface _surface;
    private float _radius;

    public Action<WashStage> StrokeStarted;

    public WashingController(RectTransform washRect)
    {
        _washRect = washRect;
    }

    public void Begin(WashSurface surface, float carScale)
    {
        _surface = surface;
        _radius = 0.09f;
        _last.Clear();
        _seen.Clear();
        _stale.Clear();
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
        _radius = WashingTools.Radius(stage, _carScale);
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
    }

    public float Progress(WashStage stage)
    {
        return _surface == null ? 0f : _surface.Progress(stage);
    }

    public void FillCurrent(WashStage stage)
    {
        _last.Clear();
        if (_surface != null) _surface.FillCurrent(stage);
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
        _last[id] = uv;
    }

    private bool TryScreenToUv(Vector2 screenPosition, out Vector2 uv)
    {
        uv = Vector2.zero;
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_washRect, screenPosition, null, out local))
            return false;

        Rect r = _washRect.rect;
        float u = local.x / Mathf.Max(1f, r.width) + 0.5f;
        float v = local.y / Mathf.Max(1f, r.height) + 0.5f;
        if (u < -0.08f || u > 1.08f || v < -0.08f || v > 1.08f) return false;
        uv = new Vector2(Mathf.Clamp01(u), Mathf.Clamp01(v));
        return true;
    }
}
