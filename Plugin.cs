using BepInEx;
using HarmonyLib;
using RoR2.UI;
using System.Reflection;

[assembly: AssemblyVersion(Local.HealthBar.Viewer.Plugin.versionNumber)]

namespace Local.HealthBar.Viewer
{
	[BepInPlugin("local.healthbar.viewer", "HealthBarViewer", versionNumber)]
	public class Plugin : BaseUnityPlugin
	{
		public const string versionNumber = "0.0.1";
		public void Awake() => Harmony.CreateAndPatchAll(typeof(Plugin));

		[HarmonyPatch(typeof(CombatHealthBarViewer), nameof(CombatHealthBarViewer.Awake))]
		[HarmonyPrefix]
		private static void IncreaseDuration(CombatHealthBarViewer __instance)
				=> __instance.healthBarDuration = float.PositiveInfinity;
	}
}
