using UnityEngine;

/// <summary>
/// World objects can implement this to react to a laser trigger-click from a
/// <see cref="ControllerLaser"/>.
/// </summary>
public interface ILaserClickable
{
    void OnLaserClick(ControllerLaser laser, RaycastHit hit);
}
