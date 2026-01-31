#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

[InitializeOnLoad]
public class CheckMetaPackages
{
    const string PACKAGE_TO_SEARCH = "com.meta.xr.sdk.core";
    const string PACKAGE_TO_INSTALL = "com.meta.xr.sdk.core"/*, "com.meta.xr.sdk.platform", "com.meta.xr.sdk.audio"*/;
    const string DEFINE = "OCULUS_SDK_AVAILABLE";
    static CheckMetaPackages()
    {
        var group = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
        var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);

        if(defines.Contains(DEFINE) == false)
        {
            var packageCheck = new PackagePresenceCheck(PACKAGE_TO_SEARCH, (packageInfo) => {
                if (packageInfo != null)
                {
                    if (defines.Contains(DEFINE) == false) { defines = $"{defines};{DEFINE}"; }
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(group, defines);
                }
                else
                {
                    Debug.LogError($"For the Meta integration to work, you need to install the package {PACKAGE_TO_INSTALL}\n" +
                        $"See https://developer.oculus.com/documentation/unity/unity-package-manager/");
                    //Client.Add(PACKAGE_TO_INSTALL);
                }
            });
        }
    }
    public class PackagePresenceCheck
    {
        string packageName = null;
        ListRequest request;
        public delegate void ResultDelegate(UnityEditor.PackageManager.PackageInfo packageInfo);
        ResultDelegate resultCallback;
        public PackagePresenceCheck(string packageName, ResultDelegate resultCallback, bool useOfflineMode = true)
        {
            this.packageName = packageName;
            this.resultCallback = resultCallback;
            request = Client.List(offlineMode: useOfflineMode, includeIndirectDependencies: true);
            EditorApplication.update += Progress;
        }

        void Progress()
        {
            if (request.IsCompleted)
            {
                UnityEditor.PackageManager.PackageInfo packageInfo = null;
                if (request.Status == StatusCode.Success)
                {
                    foreach (var info in request.Result)
                    {
                        if (info.name == packageName)
                        {
                            packageInfo = info;
                            break;
                        }
                    }
                }
                resultCallback(packageInfo);
                EditorApplication.update -= Progress;
            }
        }
    }

}


#endif
