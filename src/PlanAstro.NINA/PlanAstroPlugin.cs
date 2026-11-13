using NINA.Astrometry;
using NINA.Core.Enum;
using NINA.Core.Utility.Notification;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace PlanAstro.NINA {

    /// <summary>
    /// PlanAstro for NINA. Pairs with a PlanAstro account, collects targets sent from PlanAstro
    /// and opens them in NINA's Framing Assistant.
    /// The settings page (Options.xaml) binds directly to this class.
    /// </summary>
    [Export(typeof(IPluginManifest))]
    public class PlanAstroPlugin : PluginBase, INotifyPropertyChanged {
        private readonly IProfileService profileService;
        private readonly IFramingAssistantVM framing;
        private readonly IApplicationMediator application;
        private readonly PluginSettings settings;
        private readonly PlanAstroClient client;
        private readonly CancellationTokenSource stop = new();
        private string token;

        [ImportingConstructor]
        public PlanAstroPlugin(IProfileService profileService, IFramingAssistantVM framing, IApplicationMediator application) {
            this.profileService = profileService;
            this.framing = framing;
            this.application = application;

            settings = SettingsStore.Load();
            token = SettingsStore.Unprotect(settings.EncryptedToken);
            client = new PlanAstroClient(() => settings.BaseUrl, () => token, Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0");

            Inbox = new ObservableCollection<SentTarget>(settings.Inbox.OrderByDescending(t => t.SentAt));

            PairCommand = new RelayCommand(async _ => await Pair(), _ => !Busy && !IsPaired && !string.IsNullOrWhiteSpace(PairingCode));
            DisconnectCommand = new RelayCommand(_ => Disconnect(), _ => IsPaired);
            CheckNowCommand = new RelayCommand(async _ => await CheckQueue(), _ => IsPaired && !Busy);
            OpenCommand = new RelayCommand(async p => await OpenInFraming(p as SentTarget), p => p is SentTarget);
            DismissCommand = new RelayCommand(p => Dismiss(p as SentTarget), p => p is SentTarget);
            AddTestTargetCommand = new RelayCommand(_ => AddTestTarget());

            Status = IsPaired ? $"Connected to {settings.Account}" : "Not connected";
            _ = Task.Run(() => PollLoop(stop.Token));
        }

        public override Task Teardown() {
            stop.Cancel();
            return base.Teardown();
        }

        // ---------- bound properties ----------

        public ObservableCollection<SentTarget> Inbox { get; }

        public bool IsPaired => !string.IsNullOrEmpty(token);
        public bool IsNotPaired => !IsPaired;

        private string pairingCode;
        public string PairingCode { get => pairingCode; set { pairingCode = value; Raise(); } }

        public string DeviceName {
            get => settings.DeviceName;
            set { settings.DeviceName = string.IsNullOrWhiteSpace(value) ? Environment.MachineName : value.Trim(); SettingsStore.Save(settings); Raise(); }
        }

        public string BaseUrl {
            get => settings.BaseUrl;
            set { settings.BaseUrl = string.IsNullOrWhiteSpace(value) ? "https://planastro.com" : value.Trim(); SettingsStore.Save(settings); Raise(); }
        }

        public int PollSeconds {
            get => settings.PollSeconds;
            set { settings.PollSeconds = Math.Clamp(value, 30, 600); SettingsStore.Save(settings); Raise(); }
        }

        public bool OpenNewArrivals {
            get => settings.OnArrival == ArrivalAction.OpenInFraming;
            set { settings.OnArrival = value ? ArrivalAction.OpenInFraming : ArrivalAction.InboxOnly; SettingsStore.Save(settings); Raise(); }
        }

        private string status;
        public string Status { get => status; private set { status = value; Raise(); } }

        private bool busy;
        public bool Busy { get => busy; private set { busy = value; Raise(); CommandManager.InvalidateRequerySuggested(); } }

        public ICommand PairCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand CheckNowCommand { get; }
        public ICommand OpenCommand { get; }
        public ICommand DismissCommand { get; }
        public ICommand AddTestTargetCommand { get; }

        // ---------- pairing ----------

        private async Task Pair() {
            Busy = true;
            try {
                Status = "Pairing…";
                var result = await client.Pair(PairingCode, DeviceName, stop.Token);
                token = result.Token;
                settings.EncryptedToken = SettingsStore.Protect(result.Token);
                settings.Account = result.Account;
                SettingsStore.Save(settings);
                PairingCode = "";
                Status = $"Connected to {settings.Account}";
                RaisePairing();
                Notification.ShowSuccess("PlanAstro connected. Targets you send will appear here.");
                Log.Info("Paired");
                await CheckQueue();
            } catch (Exception ex) {
                Status = ex.Message;
                Log.Error("Pairing failed", ex);
            } finally {
                Busy = false;
            }
        }

        private void Disconnect() {
            token = null;
            settings.EncryptedToken = null;
            settings.Account = null;
            SettingsStore.Save(settings);
            Status = "Not connected. To remove this PC from your account too, use Disconnect in PlanAstro (Menu > Connect NINA).";
            RaisePairing();
            Log.Info("Disconnected on this PC");
        }

        private void RaisePairing() {
            Raise(nameof(IsPaired));
            Raise(nameof(IsNotPaired));
            CommandManager.InvalidateRequerySuggested();
        }

        // ---------- polling ----------

        private async Task PollLoop(CancellationToken ct) {
            try { await Task.Delay(TimeSpan.FromSeconds(5), ct); } catch (OperationCanceledException) { return; }
            while (!ct.IsCancellationRequested) {
                if (IsPaired && !Busy) await CheckQueue();
                try { await Task.Delay(TimeSpan.FromSeconds(Math.Max(30, settings.PollSeconds)), ct); } catch (OperationCanceledException) { return; }
            }
        }

        private async Task CheckQueue() {
            if (!IsPaired) return;
            try {
                var queue = await client.GetQueue(stop.Token);
                foreach (var target in queue.Targets.Where(t => t != null && !string.IsNullOrEmpty(t.Id))) {
                    if (target.Version > 1) Log.Info($"Target {target.Id} uses format v{target.Version}; reading the fields this version knows");
                    var isNew = !Inbox.Any(t => t.Id == target.Id);
                    if (isNew) {
                        await OnUi(() => Inbox.Insert(0, target));
                        SaveInbox();
                        Notification.ShowInformation($"PlanAstro: {target.Name} arrived");
                    }
                    await client.Ack(target.Id, "delivered", null, stop.Token);
                    if (isNew && settings.OnArrival == ArrivalAction.OpenInFraming) await OpenInFraming(target);
                }
                Status = $"Connected to {settings.Account} · checked {DateTime.Now:h:mm tt}";
            } catch (NotPairedException ex) {
                Status = ex.Message;
                token = null;
                settings.EncryptedToken = null;
                SettingsStore.Save(settings);
                await OnUi(RaisePairing);
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                Status = $"Couldn't reach PlanAstro: {ex.Message} Retrying in {settings.PollSeconds} s.";
                Log.Error("Queue check failed", ex);
            }
        }

        // ---------- Framing Assistant ----------

        private async Task OpenInFraming(SentTarget target) {
            if (target == null) return;
            try {
                await OnUiAsync(async () => {
                    var profile = profileService.ActiveProfile;
                    var coords = new Coordinates(Angle.ByDegree(target.Ra), Angle.ByDegree(target.Dec), Epoch.J2000);
                    var dso = new DeepSkyObject(target.Name ?? "PlanAstro target", coords,
                        profile.ApplicationSettings.SkyAtlasImageRepository, profile.AstrometrySettings.Horizon);
                    // Rotation: NINA 3 carries the position angle on the target. Set by name so a NINA build
                    // without this property still loads the coordinates; checked against a plate solve in testing.
                    TrySet(dso, "RotationPositionAngle", target.Rotation);

                    application.ChangeTab(ApplicationTab.FRAMINGASSISTANT);
                    await framing.SetCoordinates(dso);
                    LogFramingPropertiesOnce();

                    // NINA can't load the sky image without a focal length. When NINA has none (no telescope set in
                    // the profile), use the rig sent from PlanAstro. A focal length already set in NINA always wins.
                    var rig = target.Rig;
                    if (rig != null && rig.FocalLengthMm > 0 && GetDouble(framing, "FocalLength") <= 0) {
                        TrySet(framing, "FocalLength", rig.FocalLengthMm);
                        if (rig.PixelSizeUm > 0) TrySet(framing, "CameraPixelSize", rig.PixelSizeUm);
                        if (rig.WidthPx > 0) TrySet(framing, "CameraWidth", rig.WidthPx);
                        if (rig.HeightPx > 0) TrySet(framing, "CameraHeight", rig.HeightPx);
                        Log.Info($"Used PlanAstro rig for framing: {rig.FocalLengthMm} mm, {rig.PixelSizeUm} µm, {rig.WidthPx}×{rig.HeightPx}");
                    }

                    if (GetDouble(framing, "FocalLength") > 0) {
                        await RunCommand(framing, "LoadImageCommand");
                    } else {
                        Notification.ShowWarning("PlanAstro: set your telescope's focal length in NINA (Options > Equipment > Telescope) or in PlanAstro's rig, then press Load Image.");
                    }

                    // Mosaic and rotation apply to the framing rectangle, which exists once the image has loaded.
                    if (target.IsMosaic) {
                        TrySet(framing, "HorizontalPanels", target.Mosaic.Columns);
                        TrySet(framing, "VerticalPanels", target.Mosaic.Rows);
                        TrySet(framing, "OverlapPercentage", target.Mosaic.OverlapPercent);
                    }
                    TrySetRotation(target.Rotation);
                });
                if (!string.IsNullOrEmpty(target.Id) && IsPaired) {
                    try { await client.Ack(target.Id, "imported", null, stop.Token); } catch (Exception ex) { Log.Error("Import acknowledgement failed", ex); }
                }
            } catch (Exception ex) {
                Notification.ShowError($"PlanAstro couldn't open {target.Name} in the Framing Assistant: {ex.Message}");
                Log.Error("Open in Framing Assistant failed", ex);
                if (!string.IsNullOrEmpty(target.Id) && IsPaired) {
                    try { await client.Ack(target.Id, "failed", ex.Message, stop.Token); } catch { }
                }
            }
        }

        private static double GetDouble(object owner, string property) {
            try {
                var v = owner?.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance)?.GetValue(owner);
                return v == null ? 0 : Convert.ToDouble(v);
            } catch { return 0; }
        }

        /// <summary>Runs one of the Framing Assistant's commands (for example Load Image) and waits for it when it is asynchronous.</summary>
        private static async Task RunCommand(object owner, string commandName) {
            var cmd = owner?.GetType().GetProperty(commandName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(owner);
            if (cmd == null) { Log.Info($"{commandName} not available in this NINA version"); return; }
            try {
                var executeAsync = cmd.GetType().GetMethod("ExecuteAsync", new[] { typeof(object) });
                if (executeAsync != null && executeAsync.Invoke(cmd, new object[] { null }) is Task task) { await task; return; }
                if (cmd is ICommand c && c.CanExecute(null)) c.Execute(null);
            } catch (Exception ex) { Log.Error($"{commandName} failed", ex); }
        }

        /// <summary>
        /// Rotation lives on the framing rectangle in NINA's Framing Assistant. Its exact property name differs between
        /// NINA builds, so the known names are tried in order; the result is written to NINA's log.
        /// </summary>
        private void TrySetRotation(double positionAngle) {
            foreach (var name in new[] { "Rotation", "RotationDeg", "PositionAngle" }) {
                if (TrySetQuiet(framing, name, positionAngle)) { Log.Info($"Rotation set through {name}"); return; }
            }
            var rect = framing?.GetType().GetProperty("Rectangle", BindingFlags.Public | BindingFlags.Instance)?.GetValue(framing);
            foreach (var name in new[] { "TotalRotation", "Rotation", "PositionAngle" }) {
                if (TrySetQuiet(rect, name, positionAngle)) { Log.Info($"Rotation set through Rectangle.{name}"); return; }
            }
            Log.Info("No rotation property found; see the framing property list above in this log");
        }

        private static bool TrySetQuiet(object owner, string property, object value) {
            var p = owner?.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
            if (p == null || !p.CanWrite) return false;
            try { p.SetValue(owner, Convert.ChangeType(value, p.PropertyType)); return true; } catch { return false; }
        }

        private bool loggedFraming;
        /// <summary>Writes the Framing Assistant's settable properties to NINA's log once, so the next version can target them exactly.</summary>
        private void LogFramingPropertiesOnce() {
            if (loggedFraming || framing == null) return;
            loggedFraming = true;
            try {
                var names = framing.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanWrite).Select(p => $"{p.Name}:{p.PropertyType.Name}");
                Log.Info("Framing properties: " + string.Join(", ", names));
                var rect = framing.GetType().GetProperty("Rectangle", BindingFlags.Public | BindingFlags.Instance)?.GetValue(framing);
                if (rect != null) {
                    var rnames = rect.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(p => p.CanWrite).Select(p => $"{p.Name}:{p.PropertyType.Name}");
                    Log.Info("Framing rectangle properties: " + string.Join(", ", rnames));
                }
            } catch (Exception ex) { Log.Error("Could not list framing properties", ex); }
        }

        private static void TrySet(object owner, string property, object value) {
            var p = owner?.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
            if (p == null || !p.CanWrite) { Log.Info($"{owner?.GetType().Name}.{property} not available in this NINA version"); return; }
            try { p.SetValue(owner, Convert.ChangeType(value, p.PropertyType)); } catch (Exception ex) { Log.Error($"Could not set {property}", ex); }
        }

        // ---------- inbox ----------

        private void Dismiss(SentTarget target) {
            if (target == null) return;
            Inbox.Remove(target);
            SaveInbox();
        }

        /// <summary>Adds M31 locally so the Framing Assistant hand-off can be tested before pairing.</summary>
        private void AddTestTarget() {
            var test = new SentTarget {
                Id = null,
                Name = "M31 Andromeda Galaxy (test)",
                Ra = 10.6847, Dec = 41.2690, Rotation = 35,
                Mosaic = new Mosaic { Columns = 2, Rows = 1, OverlapPercent = 15 },
                Rig = new Rig { Telescope = "Evostar 80ED + 0.8× reducer (test)", Reducer = 0.8, FocalLengthMm = 480, Camera = "ASI2600MC Pro (test)", PixelSizeUm = 3.76, WidthPx = 6248, HeightPx = 4176 },
                Note = "Local test target; not from PlanAstro",
                SentAt = DateTimeOffset.Now
            };
            Inbox.Insert(0, test);
        }

        private void SaveInbox() {
            settings.Inbox = Inbox.Where(t => !string.IsNullOrEmpty(t.Id)).Take(50).ToList();
            SettingsStore.Save(settings);
        }

        // ---------- helpers ----------

        private static Task OnUi(Action action) =>
            Application.Current?.Dispatcher.InvokeAsync(action).Task ?? Task.Run(action);

        private static Task OnUiAsync(Func<Task> action) =>
            Application.Current?.Dispatcher.InvokeAsync(action).Task.Unwrap() ?? action();

        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    internal class RelayCommand : ICommand {
        private readonly Action<object> run;
        private readonly Predicate<object> can;
        public RelayCommand(Action<object> run, Predicate<object> can = null) { this.run = run; this.can = can; }
        public bool CanExecute(object parameter) => can?.Invoke(parameter) ?? true;
        public void Execute(object parameter) => run(parameter);
        public event EventHandler CanExecuteChanged {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
