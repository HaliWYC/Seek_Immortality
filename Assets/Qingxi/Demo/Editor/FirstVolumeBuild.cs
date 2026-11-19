using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class FirstVolumeBuild
{
    [MenuItem("行旅/验证第一卷")]
    public static void Check()
    {
        HerbalStudyChecks.Run();
        FieldEncounterChecks.Run();
        TacticalGroundAuthoring.Validate();
        TravelRouteChecks.Run();
        DemoCultivationChecks.Run();
        RealmFrameworkChecks.Run();
        CultivatorBagChecks.Run();
        QingxiArtChecks.Run();
        GateLoadoutChecks.Run();
        SparInitiativeChecks.Run();
        Debug.Log("青岚山境第一卷规则检查通过。");
    }

    [MenuItem("行旅/构建第一卷 macOS 试玩版")]
    public static void Build()
    {
        Check();
        string path = Environment.GetEnvironmentVariable("QINGLAN_DEMO_OUTPUT");
        if (string.IsNullOrEmpty(path)) path = Path.GetFullPath("Builds/QinglanRoamingBattle/QinglanDemo.app");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Qingxi/Scenes/QingxiTrail.unity", "Assets/Scenes/SampleScene.unity" },
            target = BuildTarget.StandaloneOSX,
            locationPathName = path,
            options = BuildOptions.StrictMode
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("第一卷构建失败：" + report.summary.result);
#if UNITY_EDITOR_OSX
        // Unity strips the bundled frameworks to the target architecture. Seal the local
        // playtest bundle after that step so macOS can verify its nested libraries.
        SignLocalBundle(path, "--force --deep --sign - .");
        SignLocalBundle(path, "--verify --deep --strict .");
#endif
        Debug.Log("青岚山境试玩版已生成 " + path);
    }

#if UNITY_EDITOR_OSX
    static void SignLocalBundle(string bundle, string arguments)
    {
        var info = new System.Diagnostics.ProcessStartInfo("/usr/bin/codesign", arguments)
        {
            WorkingDirectory = Path.GetFullPath(bundle),
            UseShellExecute = false,
            RedirectStandardError = true
        };
        using (var process = System.Diagnostics.Process.Start(info))
        {
            string message = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new InvalidOperationException("macOS 试玩版签名失败：" + message);
        }
    }
#endif
}
