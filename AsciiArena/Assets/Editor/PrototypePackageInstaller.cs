using System;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace AsciiArena.Editor
{
    public static class PrototypePackageInstaller
    {
        private static AddAndRemoveRequest request;
        private static double deadline;

        public static void Install()
        {
            request = Client.AddAndRemove(new[]
            {
                "com.unity.entities@1.4.8",
                "com.unity.addressables@2.7.6",
                "https://github.com/Mathijs-Bakker/Extenject.git?path=UnityProject/Assets/Plugins/Zenject#47ec2e6024bbe930ec54b57a745e8e0e52e41e68"
            }, new[] { "com.unity.collab-proxy", "com.unity.multiplayer.center", "com.unity.visualscripting" });
            deadline = EditorApplication.timeSinceStartup + 600;
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (!request.IsCompleted)
            {
                if (EditorApplication.timeSinceStartup < deadline) return;
                EditorApplication.update -= Poll;
                Debug.LogError("[AsciiArena] Package installation timed out.");
                EditorApplication.Exit(2);
                return;
            }
            EditorApplication.update -= Poll;
            bool success = request.Status == StatusCode.Success;
            Debug.Log(success ? "[AsciiArena] ECS and Zenject packages resolved." : request.Error.message);
            File.WriteAllText(Path.Combine("Logs", "package-install-result.txt"), success ? "success" : request.Error.message);
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
