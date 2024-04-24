﻿using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Utility.Signatures;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using System;

namespace NotificationMaster.Notificators
{
    internal unsafe class PartyFinder : IDisposable
    {
        private NotificationMaster p;
        private int memberCount = -1;

        private delegate void ShowLogMessageDelegate(RaptureLogModule* module, uint id);
        [Signature(
            "E8 ?? ?? ?? ?? 44 03 FB ?? ?? ?? ?? ?? ?? ?? ??",
            DetourName = nameof(ShowLogMessageDetour)
        )]
        private Hook<ShowLogMessageDelegate> ShowLogMessageHook { get; init; }


        public void Dispose()
        {
            Svc.Framework.Update -= Tick;

            ShowLogMessageHook?.Disable();
            ShowLogMessageHook?.Dispose();
        }

        public PartyFinder(NotificationMaster plugin)
        {
            this.p = plugin;
            Svc.Framework.Update += Tick;

            Svc.Hook.InitializeFromAttributes(this);
            ShowLogMessageHook.Enable();
        }

        void Tick(object _)
        {
            AddonPartyList* addon = (AddonPartyList*)Svc.GameGui.GetAddonByName("_PartyList", 1);
            if (addon == null)
            {
                return;
            }

            int oldCount = memberCount;
            memberCount = addon->MemberCount;

            if (oldCount > 1 && memberCount == 1)
            {
                Notify("Party disbanded.");
                return;
            }

            if (!Svc.Condition[ConditionFlag.UsingPartyFinder] ||
                p.cfg.partyFinder_OnlyWhenFilled)
            {
                return;
            }

            if (oldCount != -1 && memberCount > 0 && oldCount != memberCount)
            {
                if (oldCount > memberCount)
                {
                    Notify("A player has left the party.");
                }
                else
                {
                    Notify("A player has joined the party.");
                }
            }
        }

        private void ShowLogMessageDetour(RaptureLogModule* module, uint id)
        {
            ShowLogMessageHook.Original(module, id);

            if (p.cfg.partyFinder_Delisted && (id == 981 || id == 982 || id == 985 || id == 986 || id == 7448))
            {
                Notify("Party recruitment ended.");
            }
            else if (id == 983 || id == 984 || id == 7451 || id == 7452)
            {
                Notify("Party recruitment ended. All places have been filled.");
            }
        }

        private void Notify(string message)
        {
            if (p.cfg.partyFinder_FlashTrayIcon)
            {
                Native.Impl.FlashWindow();
            }

            if (p.cfg.partyFinder_AutoActivateWindow)
            {
                Native.Impl.Activate();
            }

            if (p.cfg.partyFinder_ShowToastNotification)
            {
                TrayIconManager.ShowToast(message);
            }

            if (p.cfg.partyFinder_SoundSettings.PlaySound)
            {
                p.audioPlayer.Play(p.cfg.partyFinder_SoundSettings);
            }
        }

        internal static void Setup(bool enable, NotificationMaster p)
        {
            if (enable)
            {
                if (p.partyFinder == null)
                {
                    p.partyFinder = new PartyFinder(p);
                    PluginLog.Information("Enabling partyFinder module");
                }
                else
                {
                    PluginLog.Information("partyFinder module already enabled");
                }
            }
            else
            {
                if (p.partyFinder != null)
                {
                    p.partyFinder.Dispose();
                    p.partyFinder = null;
                    PluginLog.Information("Disabling partyFinder module");
                }
                else
                {
                    PluginLog.Information("partyFinder module already disabled");
                }
            }
        }
    }
}
