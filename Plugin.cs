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

[assembly: AssemblyVersion(Local.HealthBar.Viewer.Plugin.version)]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace Local.HealthBar.Viewer
{
	[BepInPlugin(identifier, "HealthBarViewer", version)]
	public class Plugin : BaseUnityPlugin
	{
		public const string version = "0.2.0", identifier = "local.healthbar.viewer";

		private class ConfigValue<T> : ConfigValue<T, T>
		{
			public ConfigValue(ConfigEntry<T> entry) : base(entry) => convert = _ => _;
			public static implicit operator ConfigValue<T>(ConfigEntry<T> entry)
					=> new(entry);
		}

		private class ConfigValue<T, U>(ConfigEntry<T> entry)
		{
			private readonly ConfigEntry<T> entry = entry;
			public Func<T, U> convert;

			public static implicit operator ConfigValue<T, U>(ConfigEntry<T> entry)
					=> new(entry);
			public static implicit operator U(ConfigValue<T, U> configuration)
					=> configuration.convert(configuration.entry.Value);
		}

		private static ConfigValue<uint> duration, range;
		private static ConfigValue<float> threshold, alpha;
		private static ConfigValue<bool> ally;
		private static ConfigValue<uint, float> delay, interval;

		public void Awake()
		{
			const string general = "General", other = "Other";

			duration = Config.Bind(
					section: general,
					key: "Minimum Duration",
					defaultValue: 10u,
					description:
						"After damage is dealt, the target's health bar will remain " +
						"visible for this many seconds."
				);

			threshold = Config.Bind(
					section: general,
					key: "Health Threshold",
					defaultValue: 75f,
					new ConfigDescription(
						"Enemies at this hit point percentage or below remain visible " +
						"indefinitely.", new AcceptableValueRange<float>(0, 100))
				);

			threshold.convert = percent;

			range = Config.Bind(
					section: general,
					key: "Maximum Range",
					defaultValue: 100u,
					description:
						"Remove health bar regardless of health threshold if distance to " +
						"target exceeds this value in meters. Set to zero for unlimited range."
				);

			ally = Config.Bind(
					section: general,
					key: "Allied Targets",
					defaultValue: true,
					description:
						"Determines whether allies reveal their target upon dealing damage."
				);

			delay = Config.Bind(
					section: other,
					key: "Targeting Delay",
					defaultValue: 0u,
					description:
						"Aiming directly at an enemy will display their health for this " +
						"many milliseconds. If target is below the health threshold, minimum " +
						"duration parameter is used instead."
				);

			delay.convert = milliseconds;

			alpha = Config.Bind(
					section: other,
					key: "Alpha Channel",
					defaultValue: 85f,
					new ConfigDescription(
						"Use this parameter to adjust transparency/opacity of the health bar " +
						"interface.", new AcceptableValueRange<float>(0, 100))
				);

			alpha.convert = percent;

			interval = Config.Bind(
					section: other,
					key: "Refresh Interval",
					defaultValue: 500u,
					description:
						"How often to check target health/range, in milliseconds. Note that " +
						"decreasing this value could negatively affect performance."
				);

			interval.convert = milliseconds;

			static float percent(float input) => input / 100f;
			static float milliseconds(uint input) => input / 1000f;

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
		[HarmonyPostfix]
		private static void ApplySettings(CombatHealthBarViewer __instance)
		{
			__instance.healthBarDuration = duration;

			if ( alpha != 1.0f && __instance.gameObject )
				__instance.gameObject.AddComponent<CanvasGroup>().alpha = alpha;
		}

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

			foreach ( CombatHealthBarViewer instance in CombatHealthBarViewer.instancesList )
			{
				if ( ! instance.viewerBodyObject )
					continue;

				object player = instance.viewerBodyObject, attacker = message.attacker;
				if ( ally )
				{
					player = instance.viewerTeamIndex;
					attacker = TeamComponent.GetObjectTeam(message.attacker);
				}

				if ( player.Equals(attacker) )
					instance.HandleDamage(target, TeamComponent.GetObjectTeam(message.victim));
			}
		}

		[HarmonyPatch(typeof(CameraRigController), nameof(CameraRigController.Update))]
		[HarmonyPostfix]
		private static void UpdateCrosshair(CameraRigController __instance)
		{
			var data = __instance.cameraMode?.camToRawInstanceData.GetValueSafe(__instance)
					as RoR2.CameraModes.CameraModePlayerBasic.InstanceData;

			__instance.lastCrosshairHurtBox = data?.lastCrosshairHurtBox;
		}

		[HarmonyPatch(typeof(CombatHealthBarViewer), nameof(CombatHealthBarViewer.Update))]
		[HarmonyPrefix]
		private static bool ShowCrosshairTarget(CombatHealthBarViewer __instance)
		{
			if ( __instance.crosshairTarget )
			{
				float delay = Plugin.delay;
				if ( __instance.crosshairTarget.combinedHealthFraction <= threshold )
					delay = duration;

				if ( delay > 0 )
				{
					CombatHealthBarViewer.HealthBarInfo
							info = __instance.GetHealthBarInfo(__instance.crosshairTarget);
					info.endTime = Mathf.Max(info.endTime, Time.time + delay);
				}
			}

			__instance.SetDirty();
			return false;
		}
	}
}
