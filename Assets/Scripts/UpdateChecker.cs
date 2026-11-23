using System;
using UnityEngine;

/// <summary>Persistent coroutine host for version checks and downloads. It is a
/// separate component so screen transitions in GameController (which call
/// StopAllCoroutines) cannot cancel a check or an installation.</summary>
public sealed class UpdateChecker : MonoBehaviour
{
    public Action<UpdateInfo, string> Checked;
    public Action<float> Progress;
    public Action<bool, string> Installed;

    public bool Busy { get; private set; }

    public void Check()
    {
        if (Busy) return;
        Busy = true;
        StartCoroutine(UpdateService.Check(delegate(UpdateInfo info, string error)
        {
            Busy = false;
            if (Checked != null) Checked(info, error);
        }));
    }

    public void Install(UpdateInfo info)
    {
        if (Busy || info == null) return;
        Busy = true;
        StartCoroutine(UpdateService.Install(info,
            delegate(float value)
            {
                if (Progress != null) Progress(value);
            },
            delegate(bool openedBrowser, string error)
            {
                Busy = false;
                if (Installed != null) Installed(openedBrowser, error);
            }));
    }
}
