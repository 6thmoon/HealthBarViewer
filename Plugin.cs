using BepInEx;
using HarmonyLib;
using RoR2;
using RoR2.UI;
using System.Reflection;
using System.Security.Permissions;
using UnityEngine;

[assembly: AssemblyVersion(Local.HealthBar.Viewer.Plugin.versionNumber)]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
		// Allow private member access via publicized assemblies.

namespace Local.HealthBar.Viewer
{
	[BepInPlugin("local.healthbar.viewer", "HealthBarViewer", versionNumber)]
	public class Plugin : BaseUnityPlugin
	{
		public const string versionNumber = "0.0.2";
		public void Awake()
		{
			Harmony.CreateAndPatchAll(typeof(Plugin));
			GlobalEventManager.onClientDamageNotified += ShowAllyTarget;
		}

		[HarmonyPatch(typeof(CombatHealthBarViewer), nameof(CombatHealthBarViewer.Awake))]
		[HarmonyPrefix]
		private static void IncreaseDuration(CombatHealthBarViewer __instance)
				=> __instance.healthBarDuration = 8;

		[HarmonyPatch(typeof(CombatHealthBarViewer), nameof(CombatHealthBarViewer.Update))]
		[HarmonyPrefix]
		private static void RemainVisible(CombatHealthBarViewer __instance)
		{
			foreach ( HealthComponent target in __instance.trackedVictims )
				if ( target.combinedHealthFraction <= 0.75f )
				{
					var healthBarInfo = __instance.GetHealthBarInfo(target);
					healthBarInfo.endTime = Mathf.Max(healthBarInfo.endTime, Time.time + 2);
				}
		}

		private static void ShowAllyTarget(DamageDealtMessage message)
		{
			if ( ! message.victim || message.isSilent ||
					TeamIndex.Player != TeamComponent.GetObjectTeam(message.attacker)
				) return;

			HealthComponent target = message.victim.GetComponent<HealthComponent>();
			if ( ! target || target.dontShowHealthbar ) return;
			TeamIndex index = TeamComponent.GetObjectTeam(message.victim);

			foreach ( CombatHealthBarViewer viewer in CombatHealthBarViewer.instancesList )
				if ( message.attacker != viewer.viewerBodyObject && viewer.viewerBodyObject )
					viewer.HandleDamage(target, index);
		}
	}
}
