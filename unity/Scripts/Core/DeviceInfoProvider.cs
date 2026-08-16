using UnityEngine;
using ZombieGame.UnityClient.Networking.Models;

namespace ZombieGame.UnityClient.Core
{
  public static class DeviceInfoProvider
  {
    public static string DeviceId =>
      string.IsNullOrWhiteSpace(SystemInfo.deviceUniqueIdentifier)
        ? System.Guid.NewGuid().ToString("N")
        : SystemInfo.deviceUniqueIdentifier;

    public static string AppVersion =>
      string.IsNullOrWhiteSpace(Application.version) ? "1.0.0" : Application.version;

    public static DevicePlatform Platform
    {
      get
      {
        switch (Application.platform)
        {
          case RuntimePlatform.IPhonePlayer:
            return DevicePlatform.iOS;
          case RuntimePlatform.Android:
            return DevicePlatform.Android;
          default:
#if UNITY_IOS
            return DevicePlatform.iOS;
#else
            return DevicePlatform.Android;
#endif
        }
      }
    }
  }
}
