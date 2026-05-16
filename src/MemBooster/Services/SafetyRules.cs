namespace MemBooster.Services;

public static class SafetyRules
{
    private static readonly HashSet<string> NamesWithoutExeExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        "system",
        "idle",
        "registry",
        "memory compression",
        "secure system",
        "system idle process"
    };

    private static readonly HashSet<string> BlockedProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "system",
        "idle",
        "registry",
        "smss.exe",
        "csrss.exe",
        "wininit.exe",
        "services.exe",
        "lsass.exe",
        "lsaiso.exe",
        "svchost.exe",
        "winlogon.exe",
        "fontdrvhost.exe",
        "dwm.exe",
        "sihost.exe",
        "taskhostw.exe",
        "taskmgr.exe",
        "conhost.exe",
        "dllhost.exe",
        "runtimebroker.exe",
        "audiodg.exe",
        "spoolsv.exe",
        "wudfhost.exe",
        "wmiprvse.exe",
        "wmiapsrv.exe",
        "aggregatorhost.exe",
        "applicationframehost.exe",
        "systemsettings.exe",
        "ctfmon.exe",
        "rundll32.exe",
        "securityhealthservice.exe",
        "securityhealthsystray.exe",
        "memory compression",
        "secure system",
        "system idle process",
        "mem-booster.exe",
        "membooster.exe",

        // Additional Windows shell/infrastructure and service processes that should never be killed directly
        "shellhost.exe",
        "ngciso.exe",
        "useroobebroker.exe",
        "sppsvc.exe",
        "sqlwriter.exe",
        "vmnat.exe",
        "vmnetdhcp.exe",
        "vmware-authd.exe",
        "vmware-usbarbitrator64.exe",
        "lockapp.exe",
};

    private static readonly HashSet<string> ManualOnlyProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Windows shell and gaming-critical helpers
        "explorer.exe",
        "startmenuexperiencehost.exe",
        "shellexperiencehost.exe",
        "searchhost.exe",
        "textinputhost.exe",
        "gamebar.exe",
        "gamebarftserver.exe",
        "gamebarpresencewriter.exe",
        "xboxapp.exe",
        "xboxpcapp.exe",
        "xboxappservices.exe",
        "xboxgamebarwidgets.exe",
        "gamingservices.exe",
        "gamingservicesnet.exe",
        "nintendoswitchonline.exe",
        "playnite.desktopapp.exe",
        "playnite.fullscreenapp.exe",
        "legendary.exe",
        "heroic.exe",

        // Generic consoles/helpers are too easy to close by mistake and usually restore poorly
        "cmd.exe",
        "powershell.exe",
        "pwsh.exe",
        "windowsterminal.exe",
        "openconsole.exe",
        "terminal.exe",
        "conhost.exe",
        "crashpad_handler.exe",
        "bravecrashhandler.exe",
        "bravecrashhandler64.exe",
        "werfault.exe",
        "werfaultsecure.exe",

        // Chat/voice/streaming often used while gaming
        "discord.exe",
        "discordcanary.exe",
        "discordptb.exe",
        "obs64.exe",
        "obs32.exe",
        "streamdeck.exe",
        "medal.exe",
        "overwolf.exe",
        "teamspeak.exe",
        "ts3client_win64.exe",

        // GPU, overlay, RGB, fan and tuning tools
        "amdsoftware.exe",
        "amd install manager.exe",
        "amdinstallmanager.exe",
        "radeonsoftware.exe",
        "amdow.exe",
        "amdrsserv.exe",
        "amdacpusrsvc.exe",
        "amdlogsr.exe",
        "amdnoise-suppression.exe",
        "nvidia app.exe",
        "nvidia overlay.exe",
        "nvidia share.exe",
        "nvidia web helper.exe",
        "nvidiashare.exe",
        "nvcontainer.exe",
        "nvdisplay.container.exe",
        "nvidia geforce experience.exe",
        "nvidia broadcast.exe",
        "afterburner.exe",
        "msiafterburner.exe",
        "rtss.exe",
        "rtsshooksloader64.exe",
        "rtsshooksloader.exe",
        "lghub.exe",
        "ghub.exe",
        "icue.exe",
        "armourycrate.exe",
        "lightingservice.exe",
        "msi center.exe",
        "msicenter.exe",
        "fancontrol.exe",
        "signalrgb.exe",
        "openrgb.exe",
        "razer synapse.exe",
        "razercentral.exe",
        "razercentralservice.exe",
        "steelseriesgg.exe",
        "sonar.exe",
        "elgato streamdeck.exe",
        "streamdeck.exe",
        "gskill.exe",
        "nzxt cam.exe",
        "cam.exe",

        // Game launchers and game services excluded from safe auto-select
        "steam.exe",
        "steamwebhelper.exe",
        "steamservice.exe",
        "epicgameslauncher.exe",
        "eadesktop.exe",
        "eabackgroundservice.exe",
        "ealink.exe",
        "link2ea.exe",
        "ubisoftconnect.exe",
        "upc.exe",
        "uplay.exe",
        "uplaywebcore.exe",
        "battle.net.exe",
        "battlenet.exe",
        "battle.net helper.exe",
        "riotclientservices.exe",
        "riotclientux.exe",
        "riotclientcrashhandler.exe",
        "goggalaxy.exe",
        "galaxyclient.exe",
        "rockstarlauncher.exe",
        "rockstarservice.exe",
        "bethesdanetlauncher.exe",
        "minecraftlauncher.exe",
        "parsec.exe",
        "moonlight.exe",
        "sunshine.exe",
        "xboxapp.exe",
        "xboxpcapp.exe",
        "xboxappservices.exe",
        "gamingservices.exe",
        "gamingservicesnet.exe",
        "gamebar.exe",
        "gamebarftserver.exe",
        "gamebarpresencewriter.exe",
        "xboxgamebarwidgets.exe",
        "vgtray.exe",
        "vgc.exe",
        "easyanticheat.exe",
        "easyanticheat_eos.exe",
        "beservice.exe",
        "faceitclient.exe",
        "faceitservice.exe",
        "battleye.exe",
        "belauncher.exe",
        "eac.exe",
        "eossdk-win64-shipping.exe",
        "fortniteclient-win64-shipping.exe",
        "valorant-win64-shipping.exe",
        "valorant.exe",
        "leagueclient.exe",
        "league of legends.exe",
        "cod.exe",
        "cod22-cod.exe",
        "modernwarfare.exe",
        "warzone.exe",
        "bf2042.exe",
        "bf6.exe",
        "helldivers2.exe",
        "cyberpunk2077.exe",
        "gta5.exe",
        "rdr2.exe",
        "minecraft.windows.exe",
        "javaw.exe",
        "processhacker.exe",
        "processhacker2.exe",

        // Monitoring, capture, LCD and local utility tools can be useful during gaming
        "cpumetricsserver.exe",
        "encoderserver.exe",
        "hwinfo.exe",
        "trafficmonitor.exe",
        "trcc.exe",
        "usblcd.exe",
        "usblcdnew.exe",

        // Virtualisation/database/license services are not game bloat. They should not be selected automatically.
        "vmnat.exe",
        "vmnetdhcp.exe",
        "vmware-authd.exe",
        "vmware-usbarbitrator64.exe",
        "sqlwriter.exe",
        "sppsvc.exe",
        "ngciso.exe",
        "useroobebroker.exe",

        // Security/VPN/driver/tuning tools excluded from automated profiles
        "avp.exe",
        "avpui.exe",
        "msmpeng.exe",
        "mssense.exe",
        "nissrv.exe",
        "senseir.exe",
        "sensecncproxy.exe",
        "simplewall.exe",
        "pia-client.exe",
        "pia-service.exe",
        "pia-wgservice.exe",
        "tailscaled.exe",
        "tailscale-ipn.exe",
        "zerotier-one_x64.exe",
        "zerotier_desktop_ui.exe",
        "openvpn.exe",
        "openvpn-gui.exe",
        "wireguard.exe",
        "wireguardtunnel.exe",
        "mullvad.exe",
        "mullvad-daemon.exe",
        "protonvpn.exe",
        "protonvpn.service.exe",
        "nordvpn.exe",
        "nordvpn-service.exe",
        "expressvpn.exe",
        "surfshark.exe",
        "amdappcompatsvc.exe",
        "amdfendrsr.exe",
        "amdppkgsvc.exe",
        "amdrssrcext.exe",
        "atieclxx.exe",
        "atiesrxx.exe",
        "cncmd.exe",
        "cpumetricsserver.exe",
        "corsairdevicecontrolservice.exe",
        "easytuneengineservice.exe",
        "gcc.exe",
        "gbt_dl_lib.exe",
        "rpmdaemon.exe",
        "rtkauduservice64.exe",
        "rtkbtmanserv.exe",
        "lghub_updater.exe",
        "logi_lamparray_service.amd64.exe",
};

    private static readonly HashSet<string> RestoreExcludedProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Restore is intentionally conservative. These are helpers, shells, crash reporters,
        // build workers, services, or short-lived child processes that should not be relaunched.
        "cmd.exe",
        "powershell.exe",
        "pwsh.exe",
        "windowsterminal.exe",
        "openconsole.exe",
        "terminal.exe",
        "conhost.exe",
        "dllhost.exe",
        "rundll32.exe",
        "crashpad_handler.exe",
        "bravecrashhandler.exe",
        "bravecrashhandler64.exe",
        "werfault.exe",
        "werfaultsecure.exe",
        "msedgewebview2.exe",
        "runtimebroker.exe",

        // Developer/build helpers
        "msbuild.exe",
        "vbcscompiler.exe",
        "vbcsccompiler.exe",
        "devhub.exe",
        "perfwatson2.exe",
        "servicehub.host.extensibility.x64.exe",
        "standardcollector.service.exe",
        "workspacelauncherforvscode.exe",

        // Office/cloud/helper services; restore the main app only, not its background workers.
        "officeclicktorun.exe",
        "filecoauth.exe",
        "filesynchelper.exe",
        "googledrivefs.exe",
        "googledrivesync.exe",
        "dropboxupdate.exe",
        "onedrivestandaloneupdater.exe",

        // PC Manager and package/background helpers. PC Manager is a packaged WindowsApps app and is slow/unreliable to relaunch by direct EXE path.
        "mspcmanager.exe",
        "mspcmanagercore.exe",
        "mspcmanagerservice.exe",
        "windowspackagemanagerserver.exe",
        "microsoftstartfeedprovider.exe",
        "widgetservice.exe",
        "appactions.exe",
        "crossdeviceresume.exe",

        // Download manager and app helper processes
        "helperservice.exe",
        "wenativehost.exe",
        "teracopyservice.exe",
        "zima-backup-v2.exe",

        // Windows/service/session processes should be left to Windows/services.msc, not relaunched like apps.
        "searchindexer.exe",
        "searchprotocolhost.exe",
        "searchfilterhost.exe",
        "ngciso.exe",
        "sppsvc.exe",
        "sqlwriter.exe",
        "useroobebroker.exe",
        "vmnat.exe",
        "vmnetdhcp.exe",
        "vmware-authd.exe",
        "vmware-usbarbitrator64.exe",

        // Monitoring/LCD tools are kept manual-only and should not be auto-restored after a boost.
        "usblcdnew.exe"
    };


    private static readonly HashSet<string> ProfileAutoLoadExcludedProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Prevent old/bad profiles from reloading service/session/utility entries that caused unstable boosts.
        "cmd.exe",
        "powershell.exe",
        "pwsh.exe",
        "windowsterminal.exe",
        "openconsole.exe",
        "terminal.exe",
        "conhost.exe",
        "dllhost.exe",
        "rundll32.exe",
        "crashpad_handler.exe",
        "bravecrashhandler.exe",
        "bravecrashhandler64.exe",
        "werfault.exe",
        "werfaultsecure.exe",
        "ngciso.exe",
        "sppsvc.exe",
        "sqlwriter.exe",
        "useroobebroker.exe",
        "lockapp.exe",
        "vmnat.exe",
        "vmnetdhcp.exe",
        "vmware-authd.exe",
        "vmware-usbarbitrator64.exe",
        "hwinfo.exe",
        "trafficmonitor.exe",
        "trcc.exe",
        "usblcd.exe",
        "usblcdnew.exe"
    };


    private static readonly HashSet<string> RecommendedGamingBoostNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Browsers
        "msedge.exe",
        "chrome.exe",
        "brave.exe",
        "firefox.exe",
        "opera.exe",
        "operagx.exe",
        "vivaldi.exe",
        "arc.exe",
        "zen.exe",
        "browser.exe",

        // Microsoft Office and productivity apps
        "winword.exe",
        "excel.exe",
        "powerpnt.exe",
        "outlook.exe",
        "onenote.exe",
        "onenotem.exe",
        "msaccess.exe",
        "mspub.exe",
        "visio.exe",
        "winproj.exe",
        "lync.exe",
        "teams.exe",
        "ms-teams.exe",
        "msteams.exe",
        "onedrive.exe",
        "sharepoint.exe",
        "officeclicktorun.exe",

        // Windows consumer/background apps
        "widgetboard.exe",
        "widgetservice.exe",
        "microsoftstartfeedprovider.exe",
        "yourphone.exe",
        "phoneexperiencehost.exe",
        "photos.exe",
        "microsoft.photos.exe",
        "copilot.exe",
        "windowscopilot.exe",
        "hxoutlook.exe",
        "hxtsr.exe",
        "microsoft.windowscommunicationsapps.exe",
        "clipchamp.exe",
        "notepad.exe",
        "mspaint.exe",
        "calculatorapp.exe",
        "screenclip.exe",
        "snippingtool.exe",

        // Work/chat apps not usually needed for gaming
        "slack.exe",
        "zoom.exe",
        "webex.exe",
        "webexmta.exe",
        "skype.exe",
        "telegram.exe",
        "whatsapp.exe",
        "messenger.exe",
        "signal.exe",
        "notion.exe",
        "obsidian.exe",
        "evernote.exe",
        "todoist.exe",
        "clickup.exe",
        "figma.exe",
        "figma agent.exe",

        // Cloud sync and media/background apps
        "dropbox.exe",
        "googledrivefs.exe",
        "googledrivesync.exe",
        "icloud.exe",
        "icloudphotos.exe",
        "applemobiledeviceservice.exe",
        "spotify.exe",
        "spotifywebhelper.exe",
        "tidal.exe",
        "deezer.exe",

        // Adobe and common update helpers
        "acrobat.exe",
        "acrocef.exe",
        "acrotray.exe",
        "adobe desktop service.exe",
        "creative cloud.exe",
        "ccxprocess.exe",
        "adobecollabsync.exe",
        "adobenotificationclient.exe",
        "acrobatnotificationclient.exe",
        "adobeupdaterservice.exe",

        // OEM assistants and support tools
        "delloptimizer.exe",
        "dell-supportassist-remedationservice.exe",
        "supportassistagent.exe",
        "hpsupportassistant.exe",
        "lenovovantage.exe",
        "lenovovantage-(genericmessagingaddin).exe"
    };

    private static readonly HashSet<string> ExtremeGamingBoostNames = new(RecommendedGamingBoostNames, StringComparer.OrdinalIgnoreCase)
    {
        // Extra Windows inbox/consumer app processes
        "msedgewebview2.exe",
        "webviewhost.exe",
        "widgets.exe",
        "newsandinterests.exe",
        "searchapp.exe",
        "microsoft.sharepoint.exe",
        "microsoft.todo.exe",
        "microsoft.notes.exe",
        "onenotedesktop.exe",
        "microsoft.whiteboard.exe",
        "whiteboard.exe",
        "people.exe",
        "maps.exe",
        "weather.exe",
        "moviesandtv.exe",
        "zunevideo.exe",
        "zunemusic.exe",
        "groove.exe",
        "cortana.exe",
        "cortanaui.exe",
        "windowscamera.exe",
        "quickassist.exe",
        "gethelp.exe",
        "mixedrealityportal.exe",

        // Extra Microsoft 365, collaboration and web wrappers
        "olk.exe",
        "microsoft365.exe",
        "msoia.exe",
        "officehubtaskhost.exe",
        "officec2rclient.exe",
        "sdxhelper.exe",
        "msosync.exe",
        "lync.exe",
        "ucmapi.exe",

        // Extra browsers, helpers and update trays
        "browsercore.exe",
        "duckduckgo.exe",
        "waterfox.exe",
        "librewolf.exe",
        "chromium.exe",
        "googleupdate.exe",
        "microsoftedgeupdate.exe",
        "braveupdate.exe",
        "opera_autoupdate.exe",
        "firefoxupdate.exe",

        // Extra work, study, dev and productivity tools
        "code.exe",
        "devenv.exe",
        "msbuild.exe",
        "vbcsccompiler.exe",
        "jetbrains.toolbox.exe",
        "rider64.exe",
        "idea64.exe",
        "pycharm64.exe",
        "webstorm64.exe",
        "postman.exe",
        "postman agent.exe",
        "insomnia.exe",
        "docker desktop.exe",
        "dockerdesktop.exe",
        "gitkraken.exe",
        "githubdesktop.exe",
        "sourcetree.exe",
        "notepad++.exe",
        "everything.exe",
        "powertoys.exe",
        "powertoys.runner.exe",
        "quicklook.exe",
        "sharex.exe",
        "greenshot.exe",
        "lightshot.exe",
        "trello.exe",
        "asana.exe",
        "monday.exe",
        "miro.exe",
        "mural.exe",

        // Extra cloud sync and vendor agents
        "box.exe",
        "boxsync.exe",
        "megasync.exe",
        "nextcloud.exe",
        "syncthingtray.exe",
        "owncloud.exe",
        "adobegcclient.exe",
        "core sync.exe",
        "adobe cef helper.exe",
        "adobecrdaemon.exe",
        "armsvc.exe",
        "adskaccess.exe",
        "autodeskdesktopapp.exe",
        "autodeskaccess.exe",
        "logioptions.exe",
        "logioptionsplus.exe",
        "logioptionsplus_agent.exe",
        "logioptionsmgr.exe",
        "dellupdatetray.exe",
        "dell.commandupdate.exe",
        "lenovosystemupdate.exe",
        "hpnotifications.exe",
        "hpjumpstartbridge.exe",
        "asussoftwaremanager.exe",
        "asus_framework.exe",

        // Extra non-game media and communication apps
        "vlc.exe",
        "itunes.exe",
        "applemusic.exe",
        "podcasts.exe",
        "netflix.exe",
        "primevideo.exe",
        "kindle.exe",
        "zoomoutlookplugin.exe",
        "teamsmachineinstaller.exe",
        "webexhost.exe",
        "skypebridge.exe",
        "line.exe",
        "viber.exe",
        "wechat.exe",
        "zoomphone.exe",

        // Known non-gaming background helpers from Windows 11 and common desktop setups
        "appactions.exe",
        "crossdeviceresume.exe",
        "mspcmanager.exe",
        "mspcmanagercore.exe",
        "mspcmanagerservice.exe",
        "filecoauth.exe",
        "filesynchelper.exe",
        "windowspackagemanagerserver.exe",
        "bravecrashhandler.exe",
        "bravecrashhandler64.exe",
        "crashpad_handler.exe",
        "microsoft.cmdpal.ui.exe",
        "microsoft.cmdpal.ext.powertoys.exe",
        "everythingcmdpal3.exe",
        "powermodecmdpal.exe",
        "baldbeardedbuilder.weatherextension.exe",
        "hoobi.bitwardencommandpaletteextension.exe",
        "jpsoftworks.toggledarkmodeextension.exe",
        "jpsoftworks.unitconverterextension.exe",
        "powertoys.advancedpaste.exe",
        "powertoys.alwaysontop.exe",
        "powertoys.awake.exe",
        "powertoys.colorpickerui.exe",
        "powertoys.cropandlock.exe",
        "powertoys.fancyzones.exe",
        "powertoys.peek.ui.exe",
        "powertoys.powerdisplay.exe",
        "powertoys.powerlauncher.exe",
        "powertoys.quickaccess.exe",
        "devhub.exe",
        "perfwatson2.exe",
        "servicehub.host.extensibility.x64.exe",
        "standardcollector.service.exe",
        "workspacelauncherforvscode.exe",
        "vbcscompiler.exe",
        "vshost.exe",
        "fdm.exe",
        "helperservice.exe",
        "wenativehost.exe",
        "flameshot.exe",
        "teracopy.exe",
        "teracopyservice.exe",
        "zima.exe",
        "zima-backup-v2.exe",
};

    private static readonly HashSet<string> AggressiveGamingSessionNames = new(ExtremeGamingBoostNames, StringComparer.OrdinalIgnoreCase)
    {
        // Aggressive gaming-session cleanup: explicit background apps, indexers, helper tools, monitoring tools and update trays.
        // These are selected only in Aggressive Select, not Safe Select.
        "searchindexer.exe",
        "searchprotocolhost.exe",
        "searchfilterhost.exe",
        "searchapp.exe",
        "widgets.exe",
        "widgetboard.exe",
        "widgetservice.exe",
        "microsoftstartfeedprovider.exe",
        "appactions.exe",
        "crossdeviceresume.exe",
        "windowscopilot.exe",
        "copilot.exe",
        "phoneexperiencehost.exe",
        "yourphone.exe",
        "mspcmanager.exe",
        "mspcmanagercore.exe",
        "mspcmanagerservice.exe",
        "windowscommandpalette.exe",
        "microsoft.cmdpal.ui.exe",
        "microsoft.cmdpal.ext.powertoys.exe",
        "everything.exe",
        "everythingcmdpal3.exe",
        "powermodecmdpal.exe",
        "baldbeardedbuilder.weatherextension.exe",
        "hoobi.bitwardencommandpaletteextension.exe",
        "jpsoftworks.toggledarkmodeextension.exe",
        "jpsoftworks.unitconverterextension.exe",
        "powertoys.exe",
        "powertoys.runner.exe",
        "powertoys.advancedpaste.exe",
        "powertoys.alwaysontop.exe",
        "powertoys.awake.exe",
        "powertoys.colorpickerui.exe",
        "powertoys.cropandlock.exe",
        "powertoys.fancyzones.exe",
        "powertoys.peek.ui.exe",
        "powertoys.powerdisplay.exe",
        "powertoys.powerlauncher.exe",
        "powertoys.quickaccess.exe",
        "googledrivefs.exe",
        "googledrivesync.exe",
        "onedrive.exe",
        "filecoauth.exe",
        "filesynchelper.exe",
        "officeclicktorun.exe",
        "msedge.exe",
        "msedgewebview2.exe",
        "chrome.exe",
        "brave.exe",
        "firefox.exe",
        "vivaldi.exe",
        "opera.exe",
        "operagx.exe",
        "devenv.exe",
        "devhub.exe",
        "perfwatson2.exe",
        "servicehub.host.extensibility.x64.exe",
        "standardcollector.service.exe",
        "workspacelauncherforvscode.exe",
        "msbuild.exe",
        "vbcscompiler.exe",
        "vbcsccompiler.exe",
        "code.exe",
        "postman.exe",
        "docker desktop.exe",
        "dockerdesktop.exe",
        "githubdesktop.exe",
        "notepad++.exe",
        "fdm.exe",
        "helperservice.exe",
        "wenativehost.exe",
        "flameshot.exe",
        "teracopy.exe",
        "teracopyservice.exe",
        "zima.exe",
        "zima-backup-v2.exe",
        "spotify.exe",
        "tidal.exe",
        "vlc.exe",
        "itunes.exe",
        "netflix.exe",
        "primevideo.exe",
        "slack.exe",
        "zoom.exe",
        "webex.exe",
        "teams.exe",
        "ms-teams.exe",
        "msteams.exe",
        "telegram.exe",
        "whatsapp.exe",
        "signal.exe",

        // Broader non-gaming workstation/productivity apps for Aggressive Select.
        // These are selected only after gaming/driver/security/core Windows exclusions are checked.
        "photoshop.exe",
        "illustrator.exe",
        "indesign.exe",
        "lightroom.exe",
        "lightroomclassic.exe",
        "lightroomcc.exe",
        "premierepro.exe",
        "adobe premiere pro.exe",
        "afterfx.exe",
        "audition.exe",
        "adobe media encoder.exe",
        "mediaencoder.exe",
        "bridge.exe",
        "adobe bridge.exe",
        "dimension.exe",
        "substance 3d painter.exe",
        "substance3dpainter.exe",
        "substance 3d designer.exe",
        "substance3ddesigner.exe",
        "creative cloud.exe",
        "creative cloud helper.exe",
        "creative cloud ui helper.exe",
        "ccxprocess.exe",
        "coresync.exe",
        "core sync.exe",
        "adobeipcbroker.exe",
        "adobe desktop service.exe",
        "adobenotificationclient.exe",
        "adobecollabsync.exe",
        "adobegcclient.exe",
        "acrobat.exe",
        "acrocef.exe",
        "acrotray.exe",
        "armsvc.exe",
        "acad.exe",
        "acadlt.exe",
        "autocad.exe",
        "revit.exe",
        "revitworker.exe",
        "inventor.exe",
        "fusion360.exe",
        "fusionlauncher.exe",
        "maya.exe",
        "maya.bin",
        "3dsmax.exe",
        "navisworks.exe",
        "roamer.exe",
        "civil3d.exe",
        "recap.exe",
        "recapviewer.exe",
        "adskaccess.exe",
        "autodeskaccess.exe",
        "autodeskdesktopapp.exe",
        "autodeskdesktopconnector.exe",
        "desktopconnector.applications.tray.exe",
        "desktopconnector.exe",
        "adskidentitymanager.exe",
        "ustation.exe",
        "microstation.exe",
        "openroadsdesigner.exe",
        "openbridge.exe",
        "openraildesigner.exe",
        "opencitiesmap.exe",
        "projectwise.exe",
        "pwc.exe",
        "connectionclient.exe",
        "bentley.connectionclient.exe",
        "bentleylicensingtool.exe",
        "canva.exe",
        "notion calendar.exe",
        "notioncalendar.exe",
        "grammarly.exe",
        "grammarly-desktop-integration.exe",
        "grammarly desktop.exe",
        "microsoft loop.exe",
        "loop.exe",
        "todo.exe",
        "msedge_proxy.exe",
        "cursor.exe",
        "windsurf.exe",
        "copilot-language-server.exe",
        "copilot.exe",
        "pycharm.exe",
        "webstorm.exe",
        "idea.exe",
        "rider.exe",
        "datagrip.exe",
        "clion.exe",
        "goland.exe",
        "rubymine.exe",
        "davinci resolve.exe",
        "resolve.exe",
        "blackmagicrawspeedtest.exe",
        "blender.exe",
        "sketchup.exe",
        "sketchupviewer.exe",
        "figma.exe",
        "figma agent.exe",
        "lunacy.exe",
        "affinity photo.exe",
        "affinity designer.exe",
        "affinity publisher.exe"
    };

    private static readonly string[] GamingRelatedNameTokens =
    {
        "steam", "epic", "eadesktop", "eaapp", "origin", "ubisoft", "uplay", "battle.net", "battlenet",
        "riot", "valorant", "league", "vanguard", "gog", "galaxy", "rockstar", "minecraft", "fortnite",
        "warzone", "modernwarfare", "callofduty", "cod", "battlefield", "bf2042", "bf6", "helldivers",
        "cyberpunk", "gta", "rdr2", "launcher", "anticheat", "anti-cheat", "easyanticheat", "battleye", "faceit",
        "xbox", "gamebar", "gamingservices", "parsec", "moonlight", "sunshine"
    };

    private static readonly string[] DriverOverlayAndTuningTokens =
    {
        "amd", "radeon", "nvidia", "nvcontainer", "geforce", "afterburner", "rtss", "rivatuner",
        "overlay", "osd", "icue", "corsair", "lghub", "ghub", "logi", "logitech", "razer", "steelseries", "sonar", "armoury",
        "aura", "lightingservice", "msicenter", "gigabyte", "easytune", "realtek", "fancontrol", "openrgb", "signalrgb", "nzxt", "cam"
    };

    private static readonly string[] SecurityNetworkProcessTokens =
    {
        "vpn", "wireguard", "openvpn", "tailscale", "zerotier", "simplewall", "firewall",
        "kaspersky", "avp", "antivirus", "defender", "msmpeng", "mssense", "security",
        "crowdstrike", "falcon", "sentinelone", "wazuh", "edr"
    };

    private static readonly string[] CoreWindowsInfrastructureTokens =
    {
        "servicehost", "service", "hostprocess", "runtimebroker", "applicationframehost", "systemsettings",
        "securityhealth", "defender", "smartscreen", "windowsdefender", "searchindexer", "trustedinstaller",
        "tiworker", "msmpeng", "sgrmbroker", "wudfhost", "wmiprvse", "spoolsv", "audiodg", "ctfmon",
        "conhost", "dllhost", "rundll32", "fontdrvhost", "dwm", "sihost", "shellhost", "shellexperiencehost",
        "startmenuexperiencehost", "taskhost", "winlogon", "wininit", "crashpad", "crashhandler", "werfault"
    };


    private static readonly string[] AggressiveNonGamingAppTokens =
    {
        // Creative/productivity suites
        "adobe", "acrobat", "creativecloud", "ccxprocess", "photoshop", "illustrator", "indesign", "lightroom",
        "premierepro", "afterfx", "mediaencoder", "audition", "substance3d", "core sync", "coresync",

        // CAD, engineering and workstation suites
        "autodesk", "autocad", "revit", "inventor", "fusion360", "navisworks", "civil3d", "3dsmax", "maya",
        "adsk", "desktopconnector", "bentley", "microstation", "openroads", "openrail", "projectwise", "connectionclient",

        // Office, collaboration, productivity and cloud sync
        "office", "microsoft365", "onenote", "outlook", "excel", "winword", "powerpnt", "teams", "onedrive",
        "sharepoint", "dropbox", "googledrive", "boxsync", "nextcloud", "slack", "zoom", "webex", "skype",

        // Work/dev/design helpers
        "visualstudio", "vscode", "githubdesktop", "gitkraken", "postman", "dockerdesktop", "jetbrains",
        "cursor", "windsurf", "copilotlanguageserver", "figma", "canva", "grammarly", "notion"
    };

    public static bool IsBlocked(string processName)
    {
        var normalised = NormaliseProcessName(processName);
        return BlockedProcessNames.Contains(normalised);
    }

    public static bool IsRecommendedForGamingBoost(string processName)
    {
        var normalised = NormaliseProcessName(processName);
        return !IsBlocked(normalised)
            && !ManualOnlyProcessNames.Contains(normalised)
            && RecommendedGamingBoostNames.Contains(normalised);
    }

    public static bool IsExtremeRecommendedForGamingBoost(string processName)
    {
        var normalised = NormaliseProcessName(processName);
        return !IsBlocked(normalised)
            && !ManualOnlyProcessNames.Contains(normalised)
            && ExtremeGamingBoostNames.Contains(normalised);
    }

    public static bool IsAggressiveRecommendedForGamingBoost(string processName)
    {
        var normalised = NormaliseProcessName(processName);
        var compact = normalised.Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty);

        if (IsBlocked(normalised) || ManualOnlyProcessNames.Contains(normalised))
        {
            return false;
        }

        if (AggressiveGamingSessionNames.Contains(normalised) || RecommendedGamingBoostNames.Contains(normalised) || ExtremeGamingBoostNames.Contains(normalised))
        {
            return true;
        }

        if (GamingRelatedNameTokens.Any(token => compact.Contains(token.Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty), StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (DriverOverlayAndTuningTokens.Any(token => compact.Contains(token.Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty), StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (SecurityNetworkProcessTokens.Any(token => compact.Contains(token.Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty), StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (CoreWindowsInfrastructureTokens.Any(token => compact.Contains(token.Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty), StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (AggressiveNonGamingAppTokens.Any(token => compact.Contains(token.Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty), StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // v0.6.13: do not auto-select fully unknown process names in Aggressive mode.
        // Unknown apps remain manually selectable/reviewable, but Aggressive can still catch known non-gaming suites by token.
        return false;
    }

    public static bool IsRestoreCandidate(string processName)
    {
        var normalised = NormaliseProcessName(processName);
        return !IsBlocked(normalised) && !RestoreExcludedProcessNames.Contains(normalised);
    }

    public static bool IsRestorePathAllowed(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        var normalisedPath = filePath.Replace('/', '\\').ToLowerInvariant();

        // Packaged WindowsApps executables are not always safe to relaunch by direct file path.
        // Microsoft PC Manager showed a ~9 second Process.Start delay/failure in diagnostics,
        // so it is intentionally skipped by automatic relaunch logic. Users can open it normally if needed.
        if (normalisedPath.Contains("\\windowsapps\\microsoft.microsoftpcmanager_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    public static bool IsKnownHelperProcess(string processName)
    {
        return RestoreExcludedProcessNames.Contains(NormaliseProcessName(processName));
    }

    public static bool IsAutoLoadProfileAllowed(string processName)
    {
        var normalised = NormaliseProcessName(processName);

        // Keep old/bad profiles from reloading service/session/utility processes that older aggressive lists selected.
        if (IsBlocked(normalised) || ProfileAutoLoadExcludedProcessNames.Contains(normalised))
        {
            return false;
        }

        return true;
    }

    public static ProcessRisk GetRisk(string processName)
    {
        var normalised = NormaliseProcessName(processName);

        if (IsBlocked(normalised))
        {
            return new ProcessRisk(false, "Protected", "Core Windows or Mem-Booster process");
        }

        if (ManualOnlyProcessNames.Contains(normalised))
        {
            return new ProcessRisk(true, "Manual only", "Excluded from safe auto-select");
        }

        if (RecommendedGamingBoostNames.Contains(normalised))
        {
            return new ProcessRisk(true, "Safe list", "Included in default boost selection");
        }

        if (ExtremeGamingBoostNames.Contains(normalised))
        {
            return new ProcessRisk(true, "Extreme", "Included only when Extreme Select is used");
        }

        if (IsAggressiveRecommendedForGamingBoost(normalised))
        {
            return new ProcessRisk(true, "Aggressive", "Included only when Aggressive Select is used");
        }

        return new ProcessRisk(true, "Review", "Selectable, but not in the default safe list");
    }

    public static string NormaliseProcessName(string processName)
    {
        var value = processName.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (NamesWithoutExeExtension.Contains(value))
        {
            return value;
        }

        return value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? value
            : value + ".exe";
    }
}

public sealed record ProcessRisk(bool CanSelect, string Label, string Description);
