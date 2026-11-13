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

        /// <summary>On: frame with the rig the target was planned with in PlanAstro. Off: use NINA's own telescope and camera.</summary>
        public bool UsePlanAstroRig {
            get => settings.UsePlanAstroRig;
            set { settings.UsePlanAstroRig = value; SettingsStore.Save(settings); Raise(); }
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
                    // Rotation: NINA 3 carries the position angle on the target and applies it after loading the image.
                    TrySet(dso, "RotationPositionAngle", target.Rotation);

                    application.ChangeTab(ApplicationTab.FRAMINGASSISTANT);
                    LogFramingPropertiesOnce();

                    // Everything is set BEFORE SetCoordinates, because SetCoordinates loads the sky image itself and
                    // then sets the rotation from dso.RotationPositionAngle (NINA 3.2 FramingAssistantVM).
                    // Loading a second time is what reset the frame in 0.1.2/0.1.3.

                    // Rig: by default the one the target was planned with in PlanAstro. With the switch off, PlanAstro's
                    // rig only fills in when NINA has no focal length at all (otherwise NINA can't load the sky image).
                    var rig = target.Rig;
                    var focal = GetDouble(framing, "FocalLength");
                    var usePlanAstroRig = settings.UsePlanAstroRig || double.IsNaN(focal) || focal <= 0;
                    if (rig != null && rig.FocalLengthMm > 0 && usePlanAstroRig) {
                        TrySet(framing, "FocalLength", rig.FocalLengthMm);
                        if (rig.PixelSizeUm > 0) TrySet(framing, "CameraPixelSize", rig.PixelSizeUm);
                        if (rig.WidthPx > 0) TrySet(framing, "CameraWidth", rig.WidthPx);
                        if (rig.HeightPx > 0) TrySet(framing, "CameraHeight", rig.HeightPx);
                        Log.Info($"Used PlanAstro rig for framing: {rig.FocalLengthMm} mm, {rig.PixelSizeUm} µm, {rig.WidthPx}×{rig.HeightPx}");
                    }
                    focal = GetDouble(framing, "FocalLength");
                    if (double.IsNaN(focal) || focal <= 0) {
                        Notification.ShowWarning("PlanAstro: set your telescope's focal length in NINA (Options > Equipment > Telescope) or in PlanAstro's rig, then press Load Image.");
                    }

                    ApplyMosaic(target);
                    FitFieldOfView(target);

                    await framing.SetCoordinates(dso);

                    // NINA rebuilds the frame when the image arrives; confirm panels, overlap and rotation held.
                    for (var attempt = 1; attempt <= 3; attempt++) {
                        if (FramingMatches(target)) { Log.Info($"Mosaic and rotation confirmed (check {attempt})"); break; }
                        Log.Info($"Framing differs from the PlanAstro plan (check {attempt}); applying again");
                        ApplyMosaic(target);
                        TrySetRotation(target.Rotation);
                        await Task.Delay(TimeSpan.FromMilliseconds(1000));
                    }
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

        private void ApplyMosaic(SentTarget target) {
            var cols = target.IsMosaic ? target.Mosaic.Columns : 1;
            var rows = target.IsMosaic ? target.Mosaic.Rows : 1;
            TrySet(framing, "HorizontalPanels", cols);
            TrySet(framing, "VerticalPanels", rows);
            if (target.IsMosaic) SetOverlapPercent(target.Mosaic.OverlapPercent);
        }

        /// <summary>
        /// Widens the sky image (Field of view) when the whole mosaic wouldn't fit in it, with room to rotate.
        /// Only ever increases it, so a wider view the user chose is kept.
        /// </summary>
        private void FitFieldOfView(SentTarget target) {
            try {
                var focal = GetDouble(framing, "FocalLength");
                var pixel = GetDouble(framing, "CameraPixelSize");
                var w = GetDouble(framing, "CameraWidth");
                var h = GetDouble(framing, "CameraHeight");
                if (focal <= 0 || pixel <= 0 || w <= 0 || h <= 0) return;
                double Deg(double px) => px * pixel / 1000.0 / focal * 180.0 / Math.PI;
                var cols = target.IsMosaic ? target.Mosaic.Columns : 1;
                var rows = target.IsMosaic ? target.Mosaic.Rows : 1;
                var overlap = target.IsMosaic ? target.Mosaic.OverlapPercent / 100.0 : 0;
                var mosaicW = Deg(w) * (cols - (cols - 1) * overlap);
                var mosaicH = Deg(h) * (rows - (rows - 1) * overlap);
                var needed = Math.Ceiling(Math.Sqrt(mosaicW * mosaicW + mosaicH * mosaicH) * 1.15 * 2) / 2; // diagonal + margin, to 0.5°
                needed = Math.Min(needed, 20);
                var current = GetDouble(framing, "FieldOfView");
                if (needed > current) {
                    TrySet(framing, "FieldOfView", needed);
                    Log.Info($"Field of view widened from {current}° to {needed}° so the {cols}×{rows} mosaic fits");
                }
            } catch (Exception ex) { Log.Error("Could not fit the field of view", ex); }
        }

        /// <summary>True when NINA shows the panels, overlap and rotation from the PlanAstro plan.</summary>
        private bool FramingMatches(SentTarget target) {
            var cols = target.IsMosaic ? target.Mosaic.Columns : 1;
            var rows = target.IsMosaic ? target.Mosaic.Rows : 1;
            var h = GetDouble(framing, "HorizontalPanels");
            var v = GetDouble(framing, "VerticalPanels");
            var ovl = GetDouble(framing, "OverlapPercentage") * 100;
            // NINA stores the rectangle rotation as 360 - position angle and shows 360 - that on screen.
            var shown = ((360 - GetDouble(framing, "RectangleTotalRotation")) % 360 + 360) % 360;
            Log.Info($"Framing now: panels {h}x{v}, overlap {ovl:0.#}%, rotation shown {shown:0.##}° (plan: {cols}x{rows}, {(target.IsMosaic ? target.Mosaic.OverlapPercent : 0)}%, {target.Rotation}°)");
            var panelsOk = (int)h == cols && (int)v == rows;
            var overlapOk = !target.IsMosaic || Math.Abs(ovl - target.Mosaic.OverlapPercent) < 0.5;
            var rotOk = Math.Abs(((shown - target.Rotation) % 360 + 540) % 360 - 180) < 0.6;
            return panelsOk && overlapOk && rotOk;
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
            // Same as NINA's own SetCoordinates: RectangleRotation = 360 - position angle.
            var value = ((360 - positionAngle) % 360 + 360) % 360;
            if (TrySetQuiet(framing, "RectangleRotation", value)) { Log.Info($"Rotation set to PA {positionAngle}° (RectangleRotation {value})"); return; }
            Log.Info("RectangleRotation not available in this NINA version");
        }

        /// <summary>
        /// NINA's overlap box is a value plus a unit (% or pixels): SelectedOverlapUnit + OverlapValue.
        /// Set the unit to % first, then the value. OverlapPercentage is only a fallback for older builds.
        /// </summary>
        private void SetOverlapPercent(double percent) {
            // NINA keeps overlap as a fraction (0.15 = 15%); the % box multiplies by 100 for display.
            TrySet(framing, "OverlapPercentage", percent / 100.0);
            // The % box only re-reads its value when the unit changes, so switch to px and back to % to refresh it.
            TrySetQuiet(framing, "SelectedOverlapUnit", "px");
            TrySetQuiet(framing, "SelectedOverlapUnit", "%");
            Log.Info($"Overlap set to {percent}%");
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
