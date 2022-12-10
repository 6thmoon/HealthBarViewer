using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using RoR2;
using RoR2.UI;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
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
		public const string versionNumber = "0.1.0";

		private static float duration, threshold, range;
		private const float interval = 2.0f;

		public void Awake()
		{
			const string section = "General";

			duration = Config.Bind(
					section: section,
					key: "Minimum Duration",
					defaultValue: 8u,
					description: "After an ally deals damage, the target's health bar will" +
						" remain visible for this many seconds."
				).Value;

			threshold = Config.Bind(
					section: section,
					key: "Health Threshold",
					defaultValue: 75f,
					new ConfigDescription(
						"Enemies at this hit point percentage (or below) remain visible" +
							" indefinitely.",
						new AcceptableValueRange<float>(0, 100))
				).Value / 100;

			range = Config.Bind(
					section: section,
					key: "Maximum Range",
					defaultValue: 120u,
					description:
						"Remove health bar regardless of health threshold if distance to " +
						"target exceeds this value in meters. Set to zero for unlimited range."
				).Value;

			ReplaceEventHandler();
			Harmony.CreateAndPatchAll(typeof(Plugin));
		}

		private static void ReplaceEventHandler()
		{
			try
			{
				Type healthBarViewer = typeof(CombatHealthBarViewer),
						eventManager = typeof(GlobalEventManager);
				string eventName = nameof(GlobalEventManager.onClientDamageNotified);

				RuntimeHelpers.RunClassConstructor(healthBarViewer.TypeHandle);

				EventInfo eventInfo = eventManager.GetEvent(eventName);
				Delegate eventHandler = eventManager.GetField(
						eventName, BindingFlags.Static | BindingFlags.NonPublic
					).GetValue(null) as Delegate;

				foreach ( Delegate subscriber in eventHandler.GetInvocationList() )
					if ( healthBarViewer == subscriber.Method.DeclaringType.DeclaringType )
					{
						eventInfo.RemoveEventHandler(null, subscriber);
						return;
					}

				throw new Exception("Unable to locate original health bar event handler.");
			}
			catch ( Exception exception )
			{
				System.Console.WriteLine(exception);
			}
			finally
			{
				GlobalEventManager.onClientDamageNotified += ShowHealthBar;
			}
		}

		[HarmonyPatch(typeof(CombatHealthBarViewer), nameof(CombatHealthBarViewer.Awake))]
		[HarmonyPrefix]
		private static void IncreaseDuration(CombatHealthBarViewer __instance)
				=> __instance.healthBarDuration = duration;

		[HarmonyPatch(typeof(CombatHealthBarViewer), nameof(CombatHealthBarViewer.CleanUp))]
		[HarmonyPrefix]
		private static bool CheckStatus(CombatHealthBarViewer __instance)
		{
			for ( int i = __instance.trackedVictims.Count; --i >= 0; )
			{
				HealthComponent target = __instance.trackedVictims[i];
				CombatHealthBarViewer.HealthBarInfo
						healthBarInfo = __instance.GetHealthBarInfo(target);

				if ( target && target.alive && target.body )
				{
					if ( healthBarInfo.endTime > Time.time ) continue;

					float distance = range > 0 && __instance.viewerBody ? (
							__instance.viewerBody.corePosition - target.body.corePosition
						).magnitude : range;

					if ( target.combinedHealthFraction <= threshold && distance <= range )
					{
						healthBarInfo.endTime = Time.time + interval;
						continue;
					}
				}

				__instance.Remove(i, healthBarInfo);
			}

			return false;
		}

		private static void ShowHealthBar(DamageDealtMessage message)
		{
			if ( message.isSilent || ! message.victim || ! message.attacker ) return;

			HealthComponent target = message.victim.GetComponent<HealthComponent>();
			if ( ! target || target.dontShowHealthbar ) return;

			TeamIndex attacker = TeamComponent.GetObjectTeam(message.attacker),
					victim = TeamComponent.GetObjectTeam(message.victim);

			foreach ( CombatHealthBarViewer viewer in CombatHealthBarViewer.instancesList )
				if ( viewer.viewerBodyObject && attacker == viewer.viewerTeamIndex )
					viewer.HandleDamage(target, victim);
		}
	}
}
