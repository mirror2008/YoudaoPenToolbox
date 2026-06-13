using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using HandyControl.Controls;
using YoudaoPenToolbox.Helpers;
using YoudaoPenToolbox.Models;
using YoudaoPenToolbox.Services;
using YoudaoPenToolbox.Views;

namespace YoudaoPenToolbox.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly AdbService _adbService;
        private readonly MiniAppCliService _cliService;
        private readonly PackageService _packageService;
        private readonly DeviceMonitorService _monitorService;
        private readonly ProcessMonitorService _processMonitorService;
        private readonly ProcessControlService _processControlService;
        private readonly DeviceFileBrowserService _fileBrowserService;
        private readonly AppBackupService _appBackupService;
        private readonly DeviceWatchService _deviceWatchService;
        private readonly LoliInstallService _loliInstallService;
        private readonly AdbPersistService _adbPersistService;
        private readonly PartitionService _partitionService;
        private readonly PartitionMountService _partitionMountService;
        private CancellationTokenSource _partitionTransferCts;

        private DeviceInfo _selectedDevice;
        private DeviceStatus _currentStatus = new DeviceStatus();
        private MiniAppCommand _selectedCommand;
        private InstalledApp _selectedApp;
        private string _commandOutput = "";
        private string _statusMessage = "就绪";
        private bool _isBusy;
        private string _lastDisconnectedSerial;
        private string _lastAuthWarningSerial;
        private string _param1;
        private string _param2;
        private string _cliHelpText;
        private string _cliPreviewCommand;
        private string _cliParam1Label = "参数 1";
        private string _cliParam1Hint;
        private string _cliParam1Placeholder = "参数1";
        private string _cliParam2Label = "参数 2";
        private string _cliParam2Hint;
        private string _cliParam2Placeholder = "参数2";
        private bool _cliShowParam1;
        private bool _cliShowParam2;
        private string _adbPath;
        private string _searchAppText;
        private string _startPage = "index";
        private bool _showSystemApps = true;
        private string _appsSummaryText = "";
        private string _selectedAppDetail = "";
        private string _adbPersistSummary = "未检测";
        private string _adbTerminalInput = "";
        private string _adbTerminalOutput = "";
        private string _adbCommandPreview = "";
        private string _processSummary = "";
        private string _processCountText = "未连接设备";
        private string _processLastUpdated = "";
        private ProcessInfo _selectedProcess;
        private string _selectedProcessDetail = "";
        private string _processControlHint = "选中进程后可终结或重启";
        private string _currentRemotePath = "/";
        private string _remoteFileSummary = "未连接设备";
        private RemoteFileItem _selectedRemoteFile;
        private List<RemoteFileItem> _selectedRemoteFiles = new List<RemoteFileItem>();
        private int _remoteFileItemCount;
        private List<InstalledApp> _allInstalledApps = new List<InstalledApp>();
        private BlockPartitionInfo _selectedBlockPartition;
        private string _partitionSummary = "连接设备后将自动加载分区列表";
        private string _selectedPartitionDetail = "";
        private string _partitionTransferText = "";
        private double _partitionTransferPercent;
        private List<BlockPartitionInfo> _allBlockPartitions = new List<BlockPartitionInfo>();
        private string _partitionSearchText;
        private string _activeAbSlot;
        private bool _needsAdbUnlock;

        public MainViewModel()
        {
            _adbService = new AdbService();
            _cliService = new MiniAppCliService(_adbService);
            _packageService = new PackageService(_adbService);
            _monitorService = new DeviceMonitorService(_adbService);
            _processMonitorService = new ProcessMonitorService(_adbService);
            _processControlService = new ProcessControlService(_adbService);
            _fileBrowserService = new DeviceFileBrowserService(_adbService);
            _appBackupService = new AppBackupService(_adbService, _packageService);
            _deviceWatchService = new DeviceWatchService(_adbService);
            _loliInstallService = new LoliInstallService(new DevicePlatformService(_adbService));
            _adbPersistService = new AdbPersistService(_adbService);
            _partitionService = new PartitionService(_adbService);
            _partitionMountService = new PartitionMountService(_adbService);
            _monitorService.StatusUpdated += (_, status) =>
            {
                Application.Current.Dispatcher.Invoke(() => CurrentStatus = status);
            };
            _processMonitorService.ProcessesUpdated += (_, snapshot) =>
            {
                Application.Current.Dispatcher.Invoke(() => ApplyProcessSnapshot(snapshot));
            };
            _deviceWatchService.DevicesUpdated += (_, devices) =>
            {
                Application.Current.Dispatcher.Invoke(() => ApplyDevicesUpdate(devices));
            };

            AdbPath = _adbService.AdbPath;
            AppVersion = AppInfo.VersionText;
            Devices = new ObservableCollection<DeviceInfo>();
            InstalledApps = new ObservableCollection<InstalledApp>();
            RunningProcesses = new ObservableCollection<ProcessInfo>();
            RemoteFiles = new ObservableCollection<RemoteFileItem>();
            AvailableCommands = new ObservableCollection<MiniAppCommand>(_cliService.GetAvailableCommands());
            SelectedCommand = AvailableCommands.FirstOrDefault();
            UpdateCliUi();

            RefreshDevicesCommand = new RelayCommand(async () => await RefreshDevicesAsync(), () => !IsBusy);
            SelectDeviceCommand = new RelayCommand<DeviceInfo>(SelectDevice, d => d != null && d.State == "device");
            ExecuteCommandCommand = new RelayCommand(async () => await ExecuteSelectedCommandAsync(), CanExecuteCommand);
            RefreshAppsCommand = new RelayCommand(async () => await RefreshAppsAsync(), () => SelectedDevice != null && !IsBusy);
            RefreshAppSizesCommand = new RelayCommand(async () => await RefreshAppSizesAsync(), () => SelectedDevice != null && !IsBusy);
            InstallDroppedFileCommand = new RelayCommand<string>(async path => await InstallAmrAsync(path), _ => SelectedDevice != null && !IsBusy);
            InstallLoliAppCommand = new RelayCommand(async () => await InstallLoliAppAsync(), () => SelectedDevice != null && !IsBusy);
            UninstallAppCommand = new RelayCommand(async () => await UninstallSelectedAppAsync(), () => SelectedApp != null && SelectedDevice != null && !IsBusy);
            StartAppCommand = new RelayCommand(async () => await StartSelectedAppAsync(), () => SelectedApp != null && !IsBusy);
            BackupSelectedAppCommand = new RelayCommand(async () => await BackupSelectedAppAsync(), () => SelectedApp != null && SelectedDevice != null && !IsBusy);
            CaptureScreenCommand = new RelayCommand(async () => await CaptureScreenAsync(), () => SelectedDevice != null && !IsBusy);
            QuickMemoryAppCommand = new RelayCommand(async () => await QuickCliAsync("memoryApp"), () => SelectedDevice != null && !IsBusy);
            QuickTrimCacheCommand = new RelayCommand(async () => await QuickCliAsync("trimImageCache"), () => SelectedDevice != null && !IsBusy);
            BrowseAdbPathCommand = new RelayCommand(BrowseAdbPath);
            FillSelectedAppToCliCommand = new RelayCommand(FillSelectedAppToCli, () => SelectedApp != null);
            ClearCommandOutputCommand = new RelayCommand(() => CommandOutput = string.Empty);
            RefreshAdbPersistStatusCommand = new RelayCommand(async () => await RefreshAdbPersistStatusAsync(), () => SelectedDevice != null && !IsBusy);
            EnableAdbPersistCommand = new RelayCommand(async () => await EnableAdbPersistAsync(), () => SelectedDevice != null && !IsBusy);
            DisableAdbPersistCommand = new RelayCommand(async () => await DisableAdbPersistAsync(), () => SelectedDevice != null && !IsBusy);
            ApplyAdbAuthNowCommand = new RelayCommand(async () => await ApplyAdbAuthNowAsync(), () => SelectedDevice != null && !IsBusy);
            TestAdbPersistHookCommand = new RelayCommand(async () => await TestAdbPersistHookAsync(), () => SelectedDevice != null && !IsBusy);
            OpenAdbUnlockCommand = new RelayCommand(OpenAdbUnlock, () => !IsBusy);
            RebootDeviceCommand = new RelayCommand(async () => await RebootDeviceAsync(), () => SelectedDevice != null && !IsBusy);
            ShutdownDeviceCommand = new RelayCommand(async () => await ShutdownDeviceAsync(), () => SelectedDevice != null && !IsBusy);
            ExecuteAdbCommandCommand = new RelayCommand(async () => await ExecuteAdbCommandAsync(), CanExecuteAdbCommand);
            ClearAdbTerminalCommand = new RelayCommand(() => AdbTerminalOutput = string.Empty);
            OpenAdbShellCommand = new RelayCommand(async () => await OpenAdbShellAsync(), () => SelectedDevice != null && !IsBusy);
            KillSelectedProcessCommand = new RelayCommand(async () => await KillSelectedProcessAsync(), CanControlSelectedProcess);
            RestartSelectedProcessCommand = new RelayCommand(async () => await RestartSelectedProcessAsync(), CanControlSelectedProcess);
            RefreshRemoteFilesCommand = new RelayCommand(async () => await RefreshRemoteFilesAsync(), () => SelectedDevice != null && !IsBusy);
            GoUpRemoteDirectoryCommand = new RelayCommand(async () => await GoUpRemoteDirectoryAsync(), () => SelectedDevice != null && !IsBusy && CanGoUpRemoteDirectory);
            NavigateRemotePathCommand = new RelayCommand(async () => await NavigateRemotePathAsync(), () => SelectedDevice != null && !IsBusy);
            EnterRemoteDirectoryCommand = new RelayCommand(async () => await EnterSelectedRemoteDirectoryAsync(), CanEnterSelectedRemoteDirectory);
            DeleteRemoteFileCommand = new RelayCommand(async () => await DeleteSelectedRemoteFileAsync(), CanDeleteSelectedRemoteFile);
            BrowseUploadRemoteFilesCommand = new RelayCommand(BrowseUploadRemoteFiles, () => SelectedDevice != null && !IsBusy);
            CreateRemoteFolderCommand = new RelayCommand(async () => await CreateRemoteFolderAsync(), () => SelectedDevice != null && !IsBusy);
            RenameRemoteFileCommand = new RelayCommand(async () => await RenameSelectedRemoteFileAsync(), CanRenameSelectedRemoteFile);
            RefreshPartitionsCommand = new RelayCommand(async () => await RefreshPartitionsAsync(), () => SelectedDevice != null && !IsBusy);
            ExtractPartitionCommand = new RelayCommand(async () => await ExtractSelectedPartitionAsync(), CanOperateSelectedPartition);
            FlashPartitionCommand = new RelayCommand(async () => await FlashSelectedPartitionAsync(), CanOperateSelectedPartition);
            CancelPartitionTransferCommand = new RelayCommand(CancelPartitionTransfer, () => _partitionTransferCts != null);
            BatchExtractPartitionsCommand = new RelayCommand(async () => await BatchExtractPartitionsAsync(), CanBatchExtractPartitions);
            BackupPresetPartitionsCommand = new RelayCommand(async () => await BackupPresetPartitionsAsync(), CanBackupPresetPartitions);
            ToggleSelectAllPartitionsCommand = new RelayCommand(ToggleSelectAllPartitions, () => BlockPartitions.Count > 0 && !IsBusy && _partitionTransferCts == null);
            MountSelectedPartitionCommand = new RelayCommand(async () => await MountSelectedPartitionAsync(), CanMountSelectedPartition);
            UnmountSelectedPartitionCommand = new RelayCommand(async () => await UnmountSelectedPartitionAsync(), CanUnmountSelectedPartition);
        }

        public ObservableCollection<BlockPartitionInfo> BlockPartitions { get; } = new ObservableCollection<BlockPartitionInfo>();
        public ObservableCollection<DeviceInfo> Devices { get; }
        public ObservableCollection<InstalledApp> InstalledApps { get; }
        public ObservableCollection<ProcessInfo> RunningProcesses { get; }
        public ObservableCollection<RemoteFileItem> RemoteFiles { get; }
        public ObservableCollection<MiniAppCommand> AvailableCommands { get; }

        public DeviceInfo SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (SetProperty(ref _selectedDevice, value))
                {
                    OnPropertyChanged(nameof(HasSelectedDevice));
                    _ = OnDeviceSelectedAsync();
                }
            }
        }

        public DeviceStatus CurrentStatus
        {
            get => _currentStatus;
            set => SetProperty(ref _currentStatus, value);
        }

        public MiniAppCommand SelectedCommand
        {
            get => _selectedCommand;
            set
            {
                if (SetProperty(ref _selectedCommand, value))
                {
                    Param1 = "";
                    Param2 = "";
                    UpdateCliUi();
                }
            }
        }

        public InstalledApp SelectedApp
        {
            get => _selectedApp;
            set
            {
                if (SetProperty(ref _selectedApp, value))
                {
                    UpdateSelectedAppDetail();
                    CommandManagerHelper.Invalidate();
                }
            }
        }

        public string CommandOutput
        {
            get => _commandOutput;
            set => SetProperty(ref _commandOutput, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    CommandManagerHelper.Invalidate();
                }
            }
        }

        public bool HasSelectedDevice => SelectedDevice != null;

        public string Param1
        {
            get => _param1;
            set
            {
                if (SetProperty(ref _param1, value))
                {
                    UpdateCliPreview();
                }
            }
        }

        public string Param2
        {
            get => _param2;
            set
            {
                if (SetProperty(ref _param2, value))
                {
                    UpdateCliPreview();
                }
            }
        }

        public string CliHelpText
        {
            get => _cliHelpText;
            set => SetProperty(ref _cliHelpText, value);
        }

        public string CliPreviewCommand
        {
            get => _cliPreviewCommand;
            set => SetProperty(ref _cliPreviewCommand, value);
        }

        public string CliParam1Label
        {
            get => _cliParam1Label;
            set => SetProperty(ref _cliParam1Label, value);
        }

        public string CliParam1Hint
        {
            get => _cliParam1Hint;
            set => SetProperty(ref _cliParam1Hint, value);
        }

        public string CliParam1Placeholder
        {
            get => _cliParam1Placeholder;
            set => SetProperty(ref _cliParam1Placeholder, value);
        }

        public string CliParam2Label
        {
            get => _cliParam2Label;
            set => SetProperty(ref _cliParam2Label, value);
        }

        public string CliParam2Hint
        {
            get => _cliParam2Hint;
            set => SetProperty(ref _cliParam2Hint, value);
        }

        public string CliParam2Placeholder
        {
            get => _cliParam2Placeholder;
            set => SetProperty(ref _cliParam2Placeholder, value);
        }

        public bool CliShowParam1
        {
            get => _cliShowParam1;
            set => SetProperty(ref _cliShowParam1, value);
        }

        public bool CliShowParam2
        {
            get => _cliShowParam2;
            set => SetProperty(ref _cliShowParam2, value);
        }

        public string AdbPath
        {
            get => _adbPath;
            set
            {
                if (SetProperty(ref _adbPath, value))
                {
                    _adbService.AdbPath = value;
                }
            }
        }

        public string AppVersion { get; }

        public bool NeedsAdbUnlock
        {
            get => _needsAdbUnlock;
            set => SetProperty(ref _needsAdbUnlock, value);
        }

        public string SearchAppText
        {
            get => _searchAppText;
            set => SetProperty(ref _searchAppText, value);
        }

        public string StartPage
        {
            get => _startPage;
            set => SetProperty(ref _startPage, value);
        }

        public bool ShowSystemApps
        {
            get => _showSystemApps;
            set
            {
                if (SetProperty(ref _showSystemApps, value))
                {
                    ApplyAppFilterLocal();
                }
            }
        }

        public string AppsSummaryText
        {
            get => _appsSummaryText;
            set => SetProperty(ref _appsSummaryText, value);
        }

        public string SelectedAppDetail
        {
            get => _selectedAppDetail;
            set => SetProperty(ref _selectedAppDetail, value);
        }

        public string AdbPersistSummary
        {
            get => _adbPersistSummary;
            set => SetProperty(ref _adbPersistSummary, value);
        }

        public string AdbTerminalInput
        {
            get => _adbTerminalInput;
            set
            {
                if (SetProperty(ref _adbTerminalInput, value))
                {
                    UpdateAdbCommandPreview();
                }
            }
        }

        public string AdbTerminalOutput
        {
            get => _adbTerminalOutput;
            set => SetProperty(ref _adbTerminalOutput, value);
        }

        public string AdbCommandPreview
        {
            get => _adbCommandPreview;
            set => SetProperty(ref _adbCommandPreview, value);
        }

        public string ProcessSummary
        {
            get => _processSummary;
            set => SetProperty(ref _processSummary, value);
        }

        public string ProcessCountText
        {
            get => _processCountText;
            set => SetProperty(ref _processCountText, value);
        }

        public string ProcessLastUpdated
        {
            get => _processLastUpdated;
            set => SetProperty(ref _processLastUpdated, value);
        }

        public ProcessInfo SelectedProcess
        {
            get => _selectedProcess;
            set
            {
                if (SetProperty(ref _selectedProcess, value))
                {
                    UpdateSelectedProcessDetail();
                    CommandManagerHelper.Invalidate();
                }
            }
        }

        public string SelectedProcessDetail
        {
            get => _selectedProcessDetail;
            set => SetProperty(ref _selectedProcessDetail, value);
        }

        public string ProcessControlHint
        {
            get => _processControlHint;
            set => SetProperty(ref _processControlHint, value);
        }

        public string CurrentRemotePath
        {
            get => _currentRemotePath;
            set
            {
                if (SetProperty(ref _currentRemotePath, value))
                {
                    OnPropertyChanged(nameof(CanGoUpRemoteDirectory));
                    CommandManagerHelper.Invalidate();
                }
            }
        }

        public string RemoteFileSummary
        {
            get => _remoteFileSummary;
            set => SetProperty(ref _remoteFileSummary, value);
        }

        public bool CanGoUpRemoteDirectory => RemotePathHelper.Normalize(CurrentRemotePath) != "/";

        public RemoteFileItem SelectedRemoteFile
        {
            get => _selectedRemoteFile;
            set
            {
                if (SetProperty(ref _selectedRemoteFile, value))
                {
                    CommandManagerHelper.Invalidate();
                }
            }
        }

        public int SelectedRemoteFileCount => _selectedRemoteFiles.Count;

        public RelayCommand RefreshDevicesCommand { get; }
        public RelayCommand<DeviceInfo> SelectDeviceCommand { get; }
        public RelayCommand ExecuteCommandCommand { get; }
        public RelayCommand RefreshAppsCommand { get; }
        public RelayCommand RefreshAppSizesCommand { get; }
        public RelayCommand<string> InstallDroppedFileCommand { get; }
        public RelayCommand InstallLoliAppCommand { get; }
        public RelayCommand UninstallAppCommand { get; }
        public RelayCommand StartAppCommand { get; }
        public RelayCommand BackupSelectedAppCommand { get; }
        public RelayCommand CaptureScreenCommand { get; }
        public RelayCommand QuickMemoryAppCommand { get; }
        public RelayCommand QuickTrimCacheCommand { get; }
        public RelayCommand BrowseAdbPathCommand { get; }
        public RelayCommand FillSelectedAppToCliCommand { get; }
        public RelayCommand ClearCommandOutputCommand { get; }
        public RelayCommand RefreshAdbPersistStatusCommand { get; }
        public RelayCommand EnableAdbPersistCommand { get; }
        public RelayCommand DisableAdbPersistCommand { get; }
        public RelayCommand ApplyAdbAuthNowCommand { get; }
        public RelayCommand TestAdbPersistHookCommand { get; }
        public RelayCommand OpenAdbUnlockCommand { get; }
        public RelayCommand RebootDeviceCommand { get; }
        public RelayCommand ShutdownDeviceCommand { get; }
        public RelayCommand ExecuteAdbCommandCommand { get; }
        public RelayCommand ClearAdbTerminalCommand { get; }
        public RelayCommand OpenAdbShellCommand { get; }
        public RelayCommand KillSelectedProcessCommand { get; }
        public RelayCommand RestartSelectedProcessCommand { get; }
        public RelayCommand RefreshRemoteFilesCommand { get; }
        public RelayCommand GoUpRemoteDirectoryCommand { get; }
        public RelayCommand NavigateRemotePathCommand { get; }
        public RelayCommand EnterRemoteDirectoryCommand { get; }
        public RelayCommand DeleteRemoteFileCommand { get; }
        public RelayCommand BrowseUploadRemoteFilesCommand { get; }
        public RelayCommand CreateRemoteFolderCommand { get; }
        public RelayCommand RenameRemoteFileCommand { get; }
        public RelayCommand RefreshPartitionsCommand { get; }
        public RelayCommand ExtractPartitionCommand { get; }
        public RelayCommand FlashPartitionCommand { get; }
        public RelayCommand CancelPartitionTransferCommand { get; }
        public RelayCommand BatchExtractPartitionsCommand { get; }
        public RelayCommand BackupPresetPartitionsCommand { get; }
        public RelayCommand ToggleSelectAllPartitionsCommand { get; }
        public RelayCommand MountSelectedPartitionCommand { get; }
        public RelayCommand UnmountSelectedPartitionCommand { get; }

        public string PartitionSearchText
        {
            get => _partitionSearchText;
            set
            {
                if (SetProperty(ref _partitionSearchText, value))
                {
                    ApplyPartitionFilterLocal();
                }
            }
        }

        public int SelectedPartitionBatchCount => _allBlockPartitions.Count(p => p.IsSelectedForBatch);

        public string ActiveAbSlotDisplay => string.IsNullOrWhiteSpace(_activeAbSlot)
            ? "未知"
            : _activeAbSlot.ToUpperInvariant();

        public BlockPartitionInfo SelectedBlockPartition
        {
            get => _selectedBlockPartition;
            set
            {
                if (SetProperty(ref _selectedBlockPartition, value))
                {
                    UpdateSelectedPartitionDetail();
                    _ = UpdateSelectedPartitionMountHintsAsync();
                    CommandManagerHelper.Invalidate();
                }
            }
        }

        public string PartitionSummary
        {
            get => _partitionSummary;
            set => SetProperty(ref _partitionSummary, value);
        }

        public string SelectedPartitionDetail
        {
            get => _selectedPartitionDetail;
            set => SetProperty(ref _selectedPartitionDetail, value);
        }

        public string PartitionTransferText
        {
            get => _partitionTransferText;
            set => SetProperty(ref _partitionTransferText, value);
        }

        public double PartitionTransferPercent
        {
            get => _partitionTransferPercent;
            set => SetProperty(ref _partitionTransferPercent, value);
        }

        public bool IsPartitionTransferring => _partitionTransferCts != null;

        public async Task InitializeAsync()
        {
            await RefreshDevicesAsync(showBusy: true);
            _deviceWatchService.Start();
        }

        private async Task RefreshDevicesAsync(bool showBusy = true)
        {
            if (showBusy)
            {
                IsBusy = true;
                StatusMessage = "正在扫描设备...";
            }

            try
            {
                var devices = await _adbService.GetDevicesAsync();
                ApplyDevicesUpdate(devices);

                if (SelectedDevice == null || !Devices.Any(d => d.Serial == SelectedDevice.Serial))
                {
                    SelectedDevice = Devices.FirstOrDefault(d => d.State == "device") ?? Devices.FirstOrDefault();
                }

                if (showBusy)
                {
                    StatusMessage = Devices.Count > 0
                        ? $"发现 {Devices.Count} 台设备"
                        : "未发现设备，请连接词典笔";
                }
            }
            catch (Exception ex)
            {
                if (showBusy)
                {
                    StatusMessage = $"扫描失败: {ex.Message}";
                    Growl.Warning($"ADB 扫描失败: {ex.Message}");
                }
            }
            finally
            {
                if (showBusy)
                {
                    IsBusy = false;
                }
            }
        }

        private void ApplyDevicesUpdate(IReadOnlyList<DeviceInfo> incoming)
        {
            if (incoming == null)
            {
                return;
            }

            var selectedSerial = SelectedDevice?.Serial;
            var selectedWasOnline = SelectedDevice?.State == "device";
            var incomingBySerial = incoming.ToDictionary(d => d.Serial);

            for (var i = Devices.Count - 1; i >= 0; i--)
            {
                var existing = Devices[i];
                if (!incomingBySerial.TryGetValue(existing.Serial, out var updated))
                {
                    Devices.RemoveAt(i);
                    continue;
                }

                if (updated.State != "device")
                {
                    Devices.RemoveAt(i);
                    continue;
                }

                existing.State = updated.State;
                if (!string.IsNullOrWhiteSpace(updated.Model))
                {
                    existing.Model = updated.Model;
                }

                if (!string.IsNullOrWhiteSpace(updated.ProductName))
                {
                    existing.ProductName = updated.ProductName;
                }
            }

            foreach (var device in incoming)
            {
                if (device.State != "device")
                {
                    continue;
                }

                if (Devices.Any(d => d.Serial == device.Serial))
                {
                    continue;
                }

                Devices.Add(device);
                _ = EnrichDeviceInfoAsync(device);

                if (device.Serial == _lastDisconnectedSerial)
                {
                    SelectedDevice = device;
                    HandleSelectedDeviceReconnected(device);
                }
            }

            if (selectedSerial != null && !Devices.Any(d => d.Serial == selectedSerial))
            {
                if (selectedWasOnline)
                {
                    HandleSelectedDeviceDisconnected(selectedSerial);
                }

                SelectedDevice = Devices.FirstOrDefault();
            }
            else if (SelectedDevice == null && Devices.Count > 0)
            {
                SelectedDevice = Devices.FirstOrDefault();
            }

            OnPropertyChanged(nameof(HasSelectedDevice));
        }

        private async Task EnrichDeviceInfoAsync(DeviceInfo device)
        {
            if (device == null || device.State != "device")
            {
                return;
            }

            try
            {
                var fullList = await _adbService.GetDevicesAsync().ConfigureAwait(false);
                var full = fullList.FirstOrDefault(d => d.Serial == device.Serial);
                if (full == null)
                {
                    return;
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    device.Model = full.Model;
                    device.Brand = full.Brand;
                    device.Manufacturer = full.Manufacturer;
                    device.AndroidVersion = full.AndroidVersion;
                    device.Hostname = full.Hostname;
                    device.Platform = full.Platform;
                    device.ProductName = full.ProductName;
                    OnPropertyChanged(nameof(SelectedDevice));
                });
            }
            catch
            {

            }
        }

        private void HandleSelectedDeviceDisconnected(string serial)
        {
            _monitorService.StopMonitoring();
            _processMonitorService.StopMonitoring();
            CurrentStatus = new DeviceStatus();
            InstalledApps.Clear();
            ClearProcessSnapshot();
            ClearRemoteFiles();
            ClearPartitions();
            StatusMessage = "设备已断开连接";

            if (_lastDisconnectedSerial != serial)
            {
                _lastDisconnectedSerial = serial;
                Growl.Warning("设备已断开连接");
            }
        }

        private void HandleSelectedDeviceReconnected(DeviceInfo device)
        {
            _lastDisconnectedSerial = null;
            Growl.Success("设备已重新连接");
            SelectedDevice = device;
        }

        private void SelectDevice(DeviceInfo device)
        {
            SelectedDevice = device;
        }

        private async Task OnDeviceSelectedAsync()
        {
            if (SelectedDevice == null || SelectedDevice.State != "device")
            {
                NeedsAdbUnlock = false;
                _monitorService.StopMonitoring();
                _processMonitorService.StopMonitoring();
                InstalledApps.Clear();
                CurrentStatus = new DeviceStatus();
                ClearProcessSnapshot();
                ClearRemoteFiles();
                ClearPartitions();
                return;
            }

            if (!await _adbService.IsShellAccessibleAsync(SelectedDevice.Serial).ConfigureAwait(true))
            {
                NeedsAdbUnlock = true;
                StatusMessage = "请先解锁 ADB，可通过 PenNewInject 付费解锁";
                AdbPersistSummary = "请先解锁 ADB 再来";
                if (_lastAuthWarningSerial != SelectedDevice.Serial)
                {
                    _lastAuthWarningSerial = SelectedDevice.Serial;
                    Growl.Warning("请先解锁 ADB，可通过 PenNewInject 付费解锁");
                }
            }
            else
            {
                NeedsAdbUnlock = false;
                _lastAuthWarningSerial = null;
                StatusMessage = $"已选择 {SelectedDevice.DisplayName}";
            }

            UpdateAdbCommandPreview();
            await RefreshAdbPersistStatusAsync();
            if (await _adbService.IsShellAccessibleAsync(SelectedDevice.Serial).ConfigureAwait(true))
            {
                await RefreshStatusOnceAsync();
                await RefreshAppsAsync();
                await RefreshRemoteFilesAsync();
                await RefreshPartitionsAsync();
                _monitorService.StartMonitoring(SelectedDevice.Serial);
                _processMonitorService.StartMonitoring(SelectedDevice.Serial);
            }
            else
            {
                _monitorService.StopMonitoring();
                _processMonitorService.StopMonitoring();
                InstalledApps.Clear();
                CurrentStatus = new DeviceStatus();
                ClearProcessSnapshot();
                ClearRemoteFiles();
                ClearPartitions();
            }
        }

        private void ApplyProcessSnapshot(ProcessSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            var selectedPid = SelectedProcess?.Pid ?? 0;
            var selectedIdentity = SelectedProcess?.PathDisplay ?? SelectedProcess?.Command;

            ProcessSummary = snapshot.Summary;
            RunningProcesses.Clear();
            foreach (var process in snapshot.Processes)
            {
                RunningProcesses.Add(process);
            }

            SelectedProcess = selectedPid > 0
                ? RunningProcesses.FirstOrDefault(p => p.Pid == selectedPid)
                : null;
            if (SelectedProcess == null && !string.IsNullOrWhiteSpace(selectedIdentity))
            {
                SelectedProcess = RunningProcesses.FirstOrDefault(p =>
                    string.Equals(p.PathDisplay, selectedIdentity, StringComparison.Ordinal)
                    || string.Equals(p.Command, selectedIdentity, StringComparison.Ordinal));
            }

            ProcessCountText = $"共 {snapshot.Processes.Count} 个进程（按内存占用排序）";
            ProcessLastUpdated = DateTime.Now.ToString("HH:mm:ss");
        }

        private void ClearProcessSnapshot()
        {
            RunningProcesses.Clear();
            SelectedProcess = null;
            ProcessSummary = string.Empty;
            ProcessCountText = "未连接设备";
            ProcessLastUpdated = string.Empty;
        }

        private void UpdateSelectedProcessDetail()
        {
            if (SelectedProcess == null)
            {
                SelectedProcessDetail = "未选择进程";
                ProcessControlHint = "选中进程后可终结或重启";
                return;
            }

            SelectedProcessDetail =
                $"PID: {SelectedProcess.Pid}  |  PPID: {SelectedProcess.Ppid}  |  用户: {SelectedProcess.User}  |  状态: {SelectedProcess.Stat}\r\n" +
                $"CPU: {SelectedProcess.CpuPercentDisplay}  |  内存: {SelectedProcess.MemoryPercentDisplay}  |  虚拟内存: {SelectedProcess.VirtualMemory}\r\n" +
                $"命令: {SelectedProcess.Command}\r\n" +
                $"路径: {SelectedProcess.PathDisplay}";

            if (_processControlService.IsProtectedProcess(SelectedProcess, out var reason))
            {
                ProcessControlHint = reason;
            }
            else
            {
                ProcessControlHint = "先尝试 SIGTERM，若进程未退出将自动 SIGKILL";
            }
        }

        private bool CanControlSelectedProcess()
        {
            return SelectedDevice != null
                   && SelectedProcess != null
                   && !IsBusy
                   && !_processControlService.IsProtectedProcess(SelectedProcess, out _);
        }

        private async Task RefreshProcessSnapshotAsync()
        {
            if (SelectedDevice == null)
            {
                return;
            }

            try
            {
                var snapshot = await _processMonitorService.GetSnapshotAsync(SelectedDevice.Serial).ConfigureAwait(true);
                ApplyProcessSnapshot(snapshot);
            }
            catch
            {

            }
        }

        private async Task KillSelectedProcessAsync()
        {
            if (SelectedDevice == null || SelectedProcess == null)
            {
                return;
            }

            if (_processControlService.IsProtectedProcess(SelectedProcess, out var reason))
            {
                Growl.Warning(reason);
                return;
            }

            var processName = SelectedProcess.ShortName;
            var confirm = System.Windows.MessageBox.Show(
                $"确定终结以下进程？\n\n" +
                $"PID: {SelectedProcess.Pid}\n" +
                $"命令: {SelectedProcess.Command}\n" +
                $"路径: {SelectedProcess.PathDisplay}\n\n" +
                "将先发送 SIGTERM，若进程未退出会自动 SIGKILL。",
                "终结进程",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            IsBusy = true;
            StatusMessage = $"正在终结 {processName} (PID {SelectedProcess.Pid})...";
            try
            {
                var output = await _processControlService.KillProcessAsync(SelectedDevice.Serial, SelectedProcess.Pid)
                    .ConfigureAwait(true);
                Growl.Success($"已终结进程 {processName}");
                StatusMessage = $"已终结进程 {processName}";
                if (!string.IsNullOrWhiteSpace(output))
                {
                    AppendAdbTerminalOutput($"kill {SelectedProcess.Pid}", output);
                }

                await RefreshProcessSnapshotAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Growl.Error(ex.Message);
                StatusMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RestartSelectedProcessAsync()
        {
            if (SelectedDevice == null || SelectedProcess == null)
            {
                return;
            }

            if (_processControlService.IsProtectedProcess(SelectedProcess, out var reason))
            {
                Growl.Warning(reason);
                return;
            }

            var args = await _processControlService.GetProcessArgsAsync(SelectedDevice.Serial, SelectedProcess)
                .ConfigureAwait(true);
            var startPreview = ProcessControlService.FormatArgs(args);
            if (string.IsNullOrWhiteSpace(startPreview))
            {
                Growl.Warning("无法读取进程启动命令，无法重启");
                return;
            }

            var processName = SelectedProcess.ShortName;
            var confirm = System.Windows.MessageBox.Show(
                $"确定重启以下进程？\n\n" +
                $"PID: {SelectedProcess.Pid}\n" +
                $"当前命令: {SelectedProcess.Command}\n\n" +
                $"重启命令:\n{startPreview}\n\n" +
                "将先终结原进程，再以相同命令在后台启动。",
                "重启进程",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            IsBusy = true;
            StatusMessage = $"正在重启 {processName}...";
            try
            {
                var result = await _processControlService.RestartProcessAsync(SelectedDevice.Serial, SelectedProcess)
                    .ConfigureAwait(true);
                if (result.Success)
                {
                    Growl.Success($"已重启进程 {processName}");
                    StatusMessage = $"已重启进程 {processName}";
                    AppendAdbTerminalOutput($"restart PID {SelectedProcess.Pid}", result.Message);
                }
                else
                {
                    Growl.Warning(result.Message);
                    StatusMessage = result.Message;
                }

                await RefreshProcessSnapshotAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Growl.Error(ex.Message);
                StatusMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RefreshStatusOnceAsync()
        {
            if (SelectedDevice == null)
            {
                return;
            }

            try
            {
                CurrentStatus = await _adbService.GetDeviceStatusAsync(SelectedDevice.Serial);
            }
            catch (Exception ex)
            {
                StatusMessage = $"读取状态失败: {ex.Message}";
            }
        }

        private async Task RefreshAppsAsync()
        {
            if (SelectedDevice == null)
            {
                return;
            }

            IsBusy = true;
            StatusMessage = "正在加载应用列表与占用大小...";
            try
            {
                _allInstalledApps = (await _packageService.GetInstalledAppsAsync(SelectedDevice.Serial).ConfigureAwait(true)).ToList();
                ApplyAppFilterLocal();
                UpdateAppsSummary();

                if (_allInstalledApps.Count == 0 && !string.IsNullOrWhiteSpace(_packageService.LastError))
                {
                    Growl.Warning(_packageService.LastError);
                    StatusMessage = _packageService.LastError;
                }
                else
                {
                    StatusMessage = $"已加载 {_allInstalledApps.Count} 个应用";
                }
            }
            catch (Exception ex)
            {
                Growl.Warning($"加载应用列表失败: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RefreshAppSizesAsync()
        {
            if (SelectedDevice == null || _allInstalledApps.Count == 0)
            {
                return;
            }

            IsBusy = true;
            StatusMessage = "正在重新计算应用占用...";
            try
            {
                _allInstalledApps = (await _packageService.GetInstalledAppsAsync(SelectedDevice.Serial).ConfigureAwait(true)).ToList();
                ApplyAppFilterLocal();
                UpdateAppsSummary();
                StatusMessage = "占用大小已更新";
            }
            catch (Exception ex)
            {
                Growl.Warning($"计算占用失败: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ApplyAppFilterLocal()
        {
            InstalledApps.Clear();
            foreach (var app in FilterApps(_allInstalledApps))
            {
                InstalledApps.Add(app);
            }

            UpdateAppsSummary();
        }

        private void UpdateAppsSummary()
        {
            var visible = FilterApps(_allInstalledApps).ToList();
            var totalKb = visible.Sum(a => a.SizeKb);
            var thirdParty = visible.Count(a => a.IsThirdParty);
            var protectedCount = visible.Count(a => a.IsProtectedSystemApp);
            var normalUninstall = visible.Count(a => !a.IsProtectedSystemApp);

            AppsSummaryText =
                $"显示 {visible.Count} 个应用 · 总占用 {FormatTotalSize(totalKb)} · 第三方 {thirdParty} · 受保护系统 {protectedCount} · 可卸载 {normalUninstall}";
        }

        private static string FormatTotalSize(long kb)
        {
            if (kb >= 1024 * 1024)
            {
                return $"{kb / 1024.0 / 1024.0:F2} GB";
            }

            if (kb >= 1024)
            {
                return $"{kb / 1024.0:F1} MB";
            }

            return $"{kb} KB";
        }

        private void UpdateSelectedAppDetail()
        {
            if (SelectedApp == null)
            {
                SelectedAppDetail = "";
                return;
            }

            SelectedAppDetail =
                $"名称: {SelectedApp.Name}  |  版本: {SelectedApp.Version}  |  类型: {SelectedApp.AppType}  |  占用: {SelectedApp.SizeDisplay}\r\n" +
                $"AppId: {SelectedApp.AppId}  |  分类: {SelectedApp.Category}  |  卸载: {SelectedApp.UninstallDisplay}\r\n" +
                (SelectedApp.IsProtectedSystemApp
                    ? "说明: 受保护系统应用，卸载前将自动备份 AMR，可通过拖回 AMR 尝试找回。\r\n"
                    : string.Empty) +
                $"包目录: {SelectedApp.PackageDir}\r\n" +
                $"安装路径: {SelectedApp.InstallPath}";
        }

        private IEnumerable<InstalledApp> FilterApps(IEnumerable<InstalledApp> apps)
        {
            var query = apps;

            if (!ShowSystemApps)
            {
                query = query.Where(a => a.IsThirdParty || a.CanUninstall);
            }

            if (string.IsNullOrWhiteSpace(SearchAppText))
            {
                return query;
            }

            var q = SearchAppText.Trim();
            return query.Where(a =>
                (a.Name?.IndexOf(q, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                || (a.AppId?.IndexOf(q, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                || (a.Category?.IndexOf(q, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0);
        }

        public void ApplyAppFilter()
        {
            ApplyAppFilterLocal();
        }

        private void FillSelectedAppToCli()
        {
            if (SelectedApp == null)
            {
                return;
            }

            Param1 = SelectedApp.AppId;
            if (SelectedCommand?.Name == "start")
            {
                Param2 = string.IsNullOrWhiteSpace(StartPage) ? "index" : StartPage;
            }

            StatusMessage = $"已填入应用: {SelectedApp.Name} ({SelectedApp.AppId})";
        }

        private void UpdateCliUi()
        {
            if (SelectedCommand == null)
            {
                CliHelpText = string.Empty;
                CliPreviewCommand = string.Empty;
                CliShowParam1 = false;
                CliShowParam2 = false;
                return;
            }

            CliHelpText = SelectedCommand.BuildHelpText();
            CliShowParam1 = SelectedCommand.ParameterDetails != null && SelectedCommand.ParameterDetails.Length >= 1;
            CliShowParam2 = SelectedCommand.ParameterDetails != null && SelectedCommand.ParameterDetails.Length >= 2;

            if (CliShowParam1)
            {
                var p1 = SelectedCommand.ParameterDetails[0];
                CliParam1Label = p1.Label;
                CliParam1Hint = p1.Hint;
                CliParam1Placeholder = p1.Placeholder ?? p1.Name;
            }

            if (CliShowParam2)
            {
                var p2 = SelectedCommand.ParameterDetails[1];
                CliParam2Label = p2.Label;
                CliParam2Hint = p2.Hint;
                CliParam2Placeholder = p2.Placeholder ?? p2.Name;
            }

            UpdateCliPreview();
        }

        private void UpdateCliPreview()
        {
            if (SelectedCommand == null)
            {
                CliPreviewCommand = string.Empty;
                return;
            }

            if (!SelectedCommand.RequiresParameters)
            {
                CliPreviewCommand = SelectedCommand.CommandTemplate;
                return;
            }

            var args = new string[SelectedCommand.ParameterDetails.Length];
            for (var i = 0; i < args.Length; i++)
            {
                var input = i == 0 ? Param1 : Param2;
                args[i] = string.IsNullOrWhiteSpace(input)
                    ? SelectedCommand.ParameterDetails[i].Example ?? SelectedCommand.ParameterDetails[i].Name
                    : input.Trim();
            }

            CliPreviewCommand = SelectedCommand.BuildPreview(args);
        }

        private void AppendCommandOutput(string commandName, string shellCommand, string rawOutput)
        {
            var parsed = MiniAppCliOutputFormatter.Format(commandName, rawOutput);
            var block =
                $"[{DateTime.Now:HH:mm:ss}] {commandName}\r\n" +
                $"执行命令: {shellCommand}\r\n\r\n" +
                $"▼ 解析结果\r\n{parsed}\r\n\r\n" +
                $"▼ 原始输出\r\n{(string.IsNullOrWhiteSpace(rawOutput) ? "（空）" : rawOutput.Trim())}\r\n\r\n" +
                new string('─', 48) + "\r\n\r\n";
            CommandOutput = block + CommandOutput;
        }

        private bool CanExecuteCommand()
        {
            if (SelectedDevice == null || SelectedCommand == null || IsBusy)
            {
                return false;
            }

            if (!SelectedCommand.RequiresParameters)
            {
                return true;
            }

            if (SelectedCommand.Parameters.Length == 1)
            {
                return !string.IsNullOrWhiteSpace(Param1);
            }

            return !string.IsNullOrWhiteSpace(Param1) && !string.IsNullOrWhiteSpace(Param2);
        }

        private async Task ExecuteSelectedCommandAsync()
        {
            if (SelectedDevice == null || SelectedCommand == null)
            {
                return;
            }

            IsBusy = true;
            StatusMessage = $"执行 {SelectedCommand.Name}...";
            try
            {
                string shellCommand;
                string result;
                if (SelectedCommand.RequiresParameters)
                {
                    var args = SelectedCommand.ParameterDetails.Length == 1
                        ? new[] { Param1.Trim() }
                        : new[] { Param1.Trim(), Param2.Trim() };
                    shellCommand = SelectedCommand.BuildPreview(args);
                    result = await _cliService.ExecuteAsync(SelectedDevice.Serial, SelectedCommand, args);
                }
                else
                {
                    shellCommand = SelectedCommand.CommandTemplate;
                    result = await _cliService.ExecuteAsync(SelectedDevice.Serial, SelectedCommand);
                }

                AppendCommandOutput(SelectedCommand.Name, shellCommand, result);
                StatusMessage = "命令执行完成";
            }
            catch (Exception ex)
            {
                AppendCommandOutput(SelectedCommand.Name, CliPreviewCommand, ex.Message);
                Growl.Error(ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task InstallLoliAppAsync()
        {
            if (SelectedDevice == null)
            {
                return;
            }

            IsBusy = true;
            try
            {
                StatusMessage = "正在识别设备芯片平台...";
                var platform = await _loliInstallService.DetectPlatformAsync(SelectedDevice.Serial).ConfigureAwait(true);
                if (!platform.IsSupported)
                {
                    System.Windows.MessageBox.Show(
                        $"未能自动识别设备芯片类型，无法选择安装包。\n\n{platform.DetectionDetail}",
                        "无法安装 Loli",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    StatusMessage = "设备平台识别失败";
                    return;
                }

                StatusMessage = $"已识别 {platform.PlatformLabel}，正在查询最新版本...";
                var release = await _loliInstallService.GetLatestReleaseAsync(platform).ConfigureAwait(true);

                var confirm = System.Windows.MessageBox.Show(
                    $"设备：{SelectedDevice.DisplayName}\n" +
                    $"平台：{platform.PlatformLabel}\n" +
                    $"版本：v{release.VersionText}\n" +
                    $"文件：{release.FileName}\n\n" +
                    "将从 Gitee 下载并安装，是否继续？",
                    "一键安装 Loli",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirm != MessageBoxResult.Yes)
                {
                    StatusMessage = "已取消 Loli 安装";
                    return;
                }

                StatusMessage = $"正在下载 {release.FileName}...";
                var localPath = await _loliInstallService.DownloadReleaseAsync(release).ConfigureAwait(true);

                IsBusy = false;
                await InstallAmrAsync(localPath).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Growl.Error($"Loli 安装失败: {ex.Message}");
                StatusMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task InstallAmrAsync(string localPath)
        {
            if (SelectedDevice == null || string.IsNullOrWhiteSpace(localPath))
            {
                return;
            }

            var fileName = System.IO.Path.GetFileName(localPath);
            if (!fileName.EndsWith(".amr", StringComparison.OrdinalIgnoreCase))
            {
                Growl.Warning("仅支持 .amr 格式的小程序包");
                return;
            }

            AmrPackageInfo packageInfo = null;
            string installAppId = null;
            try
            {
                packageInfo = AmrPackageService.Parse(localPath);
                installAppId = packageInfo?.AppId;
                var dialog = new Views.InstallConfirmDialog(packageInfo, SelectedDevice.DisplayName)
                {
                    Owner = Application.Current.MainWindow
                };

                if (dialog.ShowDialog() != true)
                {
                    return;
                }
            }
            finally
            {
                packageInfo?.Dispose();
            }

            IsBusy = true;
            StatusMessage = "正在上传并安装...";
            try
            {
                var remotePath = $"/userdisk/{AppBackupService.BuildSafeRemoteInstallName(installAppId)}";
                var pushed = await _adbService.PushFileAsync(SelectedDevice.Serial, localPath, remotePath);
                if (!pushed)
                {
                    throw new InvalidOperationException("上传 AMR 文件到设备失败");
                }

                var result = await _cliService.ExecuteRawAsync(SelectedDevice.Serial, $"install {remotePath}");
                var installResult = MiniAppCliResultParser.ParseInstall(result);
                var formatted = MiniAppCliOutputFormatter.Format("install", result);
                CommandOutput = $"[{DateTime.Now:HH:mm:ss}] install {fileName}\r\n{formatted}\r\n\r\n{CommandOutput}";

                if (installResult.HasJson)
                {
                    if (installResult.IsSuccess)
                    {
                        Growl.Success(installResult.Summary ?? "安装完成");
                        StatusMessage = "安装成功";
                    }
                    else
                    {
                        Growl.Warning(installResult.Summary ?? "安装失败，请查看输出");
                        StatusMessage = installResult.Summary ?? "安装失败";
                    }
                }
                else if (result.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0
                    || result.IndexOf("fail", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Growl.Warning("安装可能未成功，请查看输出");
                    StatusMessage = "安装可能未成功";
                }
                else
                {
                    Growl.Success("安装完成");
                    StatusMessage = "安装成功";
                }

                await RefreshAppsAsync();
            }
            catch (Exception ex)
            {
                Growl.Error($"安装失败: {ex.Message}");
                StatusMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task UninstallSelectedAppAsync()
        {
            if (SelectedApp == null || SelectedDevice == null)
            {
                return;
            }

            var app = SelectedApp;
            string backupPath = null;

            if (app.IsProtectedSystemApp)
            {
                if (!ConfirmProtectedSystemUninstall(app))
                {
                    return;
                }

                backupPath = AppBackupService.BuildAutoBackupPath(app);
                var backupResult = await BackupAppInternalAsync(app, backupPath, "受保护系统应用卸载前自动备份").ConfigureAwait(true);
                if (!backupResult.Success)
                {
                    var force = System.Windows.MessageBox.Show(
                        $"自动备份失败：\n{backupResult.Message ?? "未知错误"}\n\n仍要强制卸载 [{app.Name}] 吗？\n（将无法通过 AMR 找回）",
                        "备份失败",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (force != MessageBoxResult.Yes)
                    {
                        return;
                    }

                    backupPath = null;
                }
                else
                {
                    backupPath = backupResult.LocalAmrPath;
                }

                var finalConfirm = System.Windows.MessageBox.Show(
                    BuildProtectedFinalConfirmMessage(app, backupPath),
                    "确认卸载系统应用",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (finalConfirm != MessageBoxResult.Yes)
                {
                    return;
                }
            }
            else
            {
                var backupChoice = System.Windows.MessageBox.Show(
                    $"即将卸载 [{app.Name}] (ID: {app.AppId})。\n\n卸载前是否先备份 AMR？",
                    "卸载应用",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (backupChoice == MessageBoxResult.Cancel)
                {
                    return;
                }

                if (backupChoice == MessageBoxResult.Yes)
                {
                    var dlg = new Microsoft.Win32.SaveFileDialog
                    {
                        Title = "卸载前备份 AMR",
                        Filter = "AMR 小程序包|*.amr",
                        FileName = AppBackupService.BuildDefaultFileName(app)
                    };

                    if (dlg.ShowDialog() != true)
                    {
                        return;
                    }

                    var backupResult = await BackupAppInternalAsync(app, dlg.FileName, "卸载前备份").ConfigureAwait(true);
                    if (!backupResult.Success)
                    {
                        var proceed = System.Windows.MessageBox.Show(
                            $"备份失败：\n{backupResult.Message ?? "未知错误"}\n\n是否仍继续卸载？",
                            "备份失败",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);
                        if (proceed != MessageBoxResult.Yes)
                        {
                            return;
                        }
                    }
                    else
                    {
                        backupPath = backupResult.LocalAmrPath;
                    }
                }

                var confirm = System.Windows.MessageBox.Show(
                    $"确定卸载 [{app.Name}] (ID: {app.AppId})？\n\n此操作不可撤销。",
                    "确认卸载",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            await ExecuteUninstallAsync(app, backupPath).ConfigureAwait(true);
        }

        private static bool ConfirmProtectedSystemUninstall(InstalledApp app)
        {
            var backupDir = ProtectedSystemAppPolicy.GetDefaultBackupDirectory();
            var result = System.Windows.MessageBox.Show(
                $"【系统应用 · 谨慎卸载】\n\n" +
                $"「{app.Name}」属于受保护的系统应用，卸载可能导致功能异常（如录音、查词、桌面等）。\n\n" +
                $"若您执意卸载：\n" +
                $"· 将自动备份 AMR 到：\n  {backupDir}\n" +
                $"· 可通过将 AMR 拖回工具箱底部安装区尝试找回应用\n\n" +
                "是否继续？",
                "系统应用 · 谨慎卸载",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            return result == MessageBoxResult.Yes;
        }

        private static string BuildProtectedFinalConfirmMessage(InstalledApp app, string backupPath)
        {
            if (string.IsNullOrWhiteSpace(backupPath))
            {
                return $"最后确认：卸载受保护系统应用 [{app.Name}]？\n\n未成功生成备份，请谨慎操作。";
            }

            return
                $"最后确认：卸载受保护系统应用 [{app.Name}]？\n\n" +
                $"AMR 备份已保存：\n{backupPath}\n\n" +
                "如需找回，请将此 .amr 文件拖回工具箱安装。";
        }

        private async Task<AppBackupResult> BackupAppInternalAsync(InstalledApp app, string localPath, string logPrefix)
        {
            IsBusy = true;
            StatusMessage = $"正在备份 {app.Name}...";
            try
            {
                var result = await _appBackupService.BackupToAmrAsync(
                    SelectedDevice.Serial,
                    app,
                    localPath).ConfigureAwait(true);

                if (result.Success)
                {
                    CommandOutput =
                        $"[{DateTime.Now:HH:mm:ss}] {logPrefix}: {app.Name}\r\n" +
                        $"输出: {result.LocalAmrPath}\r\n" +
                        $"解析: {result.PackageInfo?.SummaryLine}\r\n\r\n{CommandOutput}";
                }

                return result;
            }
            catch (Exception ex)
            {
                return new AppBackupResult
                {
                    Success = false,
                    Message = ex.Message
                };
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteUninstallAsync(InstalledApp app, string backupPath)
        {
            IsBusy = true;
            StatusMessage = $"正在卸载 {app.Name}...";
            try
            {
                var result = await _cliService.ExecuteRawAsync(SelectedDevice.Serial, $"uninstall {app.AppId}");
                var formatted = MiniAppCliOutputFormatter.Format("uninstall", result);
                var parsed = MiniAppCliResultParser.Parse(result);
                CommandOutput = $"[{DateTime.Now:HH:mm:ss}] uninstall {app.Name}\r\n{formatted}\r\n\r\n{CommandOutput}";

                if (parsed.HasJson && parsed.ReturnCode.HasValue)
                {
                    if (parsed.IsSuccess)
                    {
                        Growl.Success($"已卸载 {app.Name}");
                    }
                    else
                    {
                        Growl.Warning(parsed.Summary ?? "卸载可能未成功，请查看输出");
                    }
                }
                else
                {
                    Growl.Info("卸载命令已发送");
                }

                if (!string.IsNullOrWhiteSpace(backupPath) && System.IO.File.Exists(backupPath))
                {
                    System.Windows.MessageBox.Show(
                        $"应用 [{app.Name}] 已执行卸载。\n\n" +
                        $"AMR 备份位置：\n{backupPath}\n\n" +
                        "如需找回应用，请将此 .amr 文件拖回工具箱底部安装区进行安装。",
                        "AMR 应用找回",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                await RefreshAppsAsync();
                StatusMessage = "卸载流程完成";
            }
            catch (Exception ex)
            {
                Growl.Error(ex.Message);
                StatusMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task StartSelectedAppAsync()
        {
            if (SelectedApp == null || SelectedDevice == null)
            {
                return;
            }

            var page = string.IsNullOrWhiteSpace(StartPage) ? "index" : StartPage.Trim();

            IsBusy = true;
            try
            {
                var result = await _cliService.ExecuteRawAsync(SelectedDevice.Serial,
                    $"start {SelectedApp.AppId} --{page}");
                CommandOutput = $"[{DateTime.Now:HH:mm:ss}] start {SelectedApp.Name}\r\n{result}\r\n\r\n{CommandOutput}";
            }
            catch (Exception ex)
            {
                Growl.Error(ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task BackupSelectedAppAsync()
        {
            if (SelectedApp == null || SelectedDevice == null)
            {
                return;
            }

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "备份小程序为 AMR",
                Filter = "AMR 小程序包|*.amr",
                FileName = AppBackupService.BuildDefaultFileName(SelectedApp)
            };

            if (dlg.ShowDialog() != true)
            {
                return;
            }

            var app = SelectedApp;
            var warning = app.IsThirdParty
                ? "将从设备安装目录打包生成 AMR，可用于同机型重新安装。"
                : "这是系统/内置应用。备份文件仅供存档，设备通常禁止通过 miniapp_cli 重复安装此类应用。";

            var confirm = System.Windows.MessageBox.Show(
                $"备份应用：{app.Name}\nAppId: {app.AppId}\n版本: {app.Version}\n\n" +
                $"安装目录：{app.InstallPath ?? app.PackageDir}\n\n{warning}\n\n是否继续？",
                "备份为 AMR",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            IsBusy = true;
            StatusMessage = $"正在备份 {app.Name}（设备端打包中）...";
            try
            {
                var result = await _appBackupService.BackupToAmrAsync(
                    SelectedDevice.Serial,
                    app,
                    dlg.FileName).ConfigureAwait(true);

                if (result.Success)
                {
                    Growl.Success(result.Message);
                    StatusMessage = result.Message;
                    System.Windows.MessageBox.Show(
                        $"应用：{app.Name}\nAppId：{app.AppId}\n\n已保存到：\n{result.LocalAmrPath}\n\n" +
                        $"包内约 {result.FileCount} 个文件，大小 {AmrPackageInfo.FormatSize(result.ArchiveSizeBytes)}。\n" +
                        "可将此 .amr 拖回工具箱安装。",
                        "备份成功",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    CommandOutput =
                        $"[{DateTime.Now:HH:mm:ss}] 备份 AMR: {app.Name}\r\n" +
                        $"输出: {result.LocalAmrPath}\r\n" +
                        $"解析: {result.PackageInfo?.SummaryLine}\r\n\r\n{CommandOutput}";
                }
                else
                {
                    Growl.Warning(result.Message ?? "备份失败");
                    StatusMessage = result.Message ?? "备份失败";
                }
            }
            catch (Exception ex)
            {
                var message = ex.Message ?? "备份失败";
                if (message.Length > 240)
                {
                    message = message.Substring(0, 240) + "...";
                }

                Growl.Error(message);
                StatusMessage = message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task CaptureScreenAsync()
        {
            if (SelectedDevice == null)
            {
                return;
            }

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG 图片|*.png",
                FileName = $"capture_{DateTime.Now:yyyyMMdd_HHmmss}.png"
            };

            if (dlg.ShowDialog() != true)
            {
                return;
            }

            IsBusy = true;
            try
            {
                var remotePath = $"/tmp/capture_{DateTime.Now.Ticks}.png";
                var result = await _cliService.ExecuteRawAsync(SelectedDevice.Serial, $"capture {remotePath}");
                var pulled = await _adbService.PullFileAsync(SelectedDevice.Serial, remotePath, dlg.FileName);
                CommandOutput = $"[{DateTime.Now:HH:mm:ss}] capture\r\n{result}\r\n保存至: {dlg.FileName}\r\n\r\n{CommandOutput}";

                if (pulled)
                {
                    Growl.Success("截图已保存");
                }
                else
                {
                    Growl.Warning("截图命令已执行，但拉取文件可能失败");
                }
            }
            catch (Exception ex)
            {
                Growl.Error(ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task QuickCliAsync(string command)
        {
            if (SelectedDevice == null)
            {
                return;
            }

            IsBusy = true;
            try
            {
                var result = await _cliService.ExecuteRawAsync(SelectedDevice.Serial, command);
                AppendCommandOutput(command, "miniapp_cli " + command, result);
            }
            catch (Exception ex)
            {
                Growl.Error(ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void BrowseAdbPath()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "adb.exe|adb.exe",
                Title = "选择 adb.exe"
            };

            if (dlg.ShowDialog() == true)
            {
                AdbPath = dlg.FileName;
            }
        }

        private void OpenAdbUnlock()
        {
            var dialog = new PenNewInjectUnlockDialog();
            if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                dialog.Owner = Application.Current.MainWindow;
            }

            dialog.ShowDialog();
        }

        private async Task RefreshAdbPersistStatusAsync()
        {
            if (SelectedDevice == null)
            {
                AdbPersistSummary = "未选择设备";
                return;
            }

            try
            {
                var status = await _adbPersistService.GetStatusAsync(SelectedDevice.Serial).ConfigureAwait(true);
                AdbPersistSummary = status.Summary;
                if (status.ShellAccessible)
                {
                    if (SelectedDevice.AndroidVersion == "需解锁 ADB")
                    {
                        await EnrichDeviceInfoAsync(SelectedDevice).ConfigureAwait(true);
                    }

                    if (InstalledApps.Count == 0 && !IsBusy)
                    {
                        await RefreshStatusOnceAsync().ConfigureAwait(true);
                        await RefreshAppsAsync().ConfigureAwait(true);
                    }
                }
            }
            catch (Exception ex)
            {
                AdbPersistSummary = $"检测失败: {ex.Message}";
            }
        }

        private async Task EnableAdbPersistAsync()
        {
            if (SelectedDevice == null)
            {
                return;
            }

            IsBusy = true;
            StatusMessage = "正在配置 ADB 持久化...";
            try
            {
                if (!await _adbService.IsShellAccessibleAsync(SelectedDevice.Serial).ConfigureAwait(true))
                {
                    throw new InvalidOperationException("ADB Shell 未通，请先解锁 ADB");
                }

                var result = await _adbPersistService.EnableAsync(SelectedDevice.Serial).ConfigureAwait(true);
                var diagnose = await _adbPersistService.DiagnoseAsync(SelectedDevice.Serial).ConfigureAwait(true);
                CommandOutput = $"[{DateTime.Now:HH:mm:ss}] 启用 ADB 持久化\r\n{result}\r\n\r\n--- 诊断 ---\r\n{diagnose}\r\n\r\n{CommandOutput}";
                Growl.Success("skip_re 持久化已配置，建议重启设备验证");
                StatusMessage = "ADB 持久化配置完成";
                await RefreshAdbPersistStatusAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Growl.Error($"配置失败: {ex.Message}");
                StatusMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task DisableAdbPersistAsync()
        {
            if (SelectedDevice == null)
            {
                return;
            }

            IsBusy = true;
            StatusMessage = "正在移除 ADB 持久化配置...";
            try
            {
                var result = await _adbPersistService.DisableAsync(SelectedDevice.Serial).ConfigureAwait(true);
                CommandOutput = $"[{DateTime.Now:HH:mm:ss}] 关闭 ADB 持久化\r\n{result}\r\n\r\n{CommandOutput}";
                Growl.Info("ADB 持久化配置已移除");
                StatusMessage = "已移除 ADB 持久化";
                await RefreshAdbPersistStatusAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Growl.Error($"移除失败: {ex.Message}");
                StatusMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ApplyAdbAuthNowAsync()
        {
            if (SelectedDevice == null)
            {
                return;
            }

            IsBusy = true;
            StatusMessage = "正在创建授权文件...";
            try
            {
                var result = await _adbPersistService.ApplyImmediateAsync(SelectedDevice.Serial).ConfigureAwait(true);
                CommandOutput = $"[{DateTime.Now:HH:mm:ss}] 立即创建授权文件\r\n{result}\r\n\r\n{CommandOutput}";
                Growl.Success("授权文件已创建（仅当前开机有效）");
                StatusMessage = "授权文件已创建";
                await RefreshAdbPersistStatusAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Growl.Error(ex.Message);
                StatusMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task TestAdbPersistHookAsync()
        {
            if (SelectedDevice == null)
            {
                return;
            }

            IsBusy = true;
            StatusMessage = "正在测试 ADB 持久化钩子...";
            try
            {
                var result = await _adbPersistService.TestHookAsync(SelectedDevice.Serial).ConfigureAwait(true);

                CommandOutput = $"[{DateTime.Now:HH:mm:ss}] 测试 ADB 持久化\r\n{result}\r\n\r\n{CommandOutput}";
                Growl.Success("测试完成，请查看输出");
                StatusMessage = "测试完成";
                await RefreshAdbPersistStatusAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Growl.Error(ex.Message);
                StatusMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool CanExecuteAdbCommand()
        {
            return SelectedDevice != null && !IsBusy && !string.IsNullOrWhiteSpace(AdbTerminalInput);
        }

        private void UpdateAdbCommandPreview()
        {
            if (SelectedDevice == null || string.IsNullOrWhiteSpace(AdbTerminalInput))
            {
                AdbCommandPreview = "adb -s <设备> <命令>";
                return;
            }

            var cmd = AdbTerminalInput.Trim();
            if (cmd.StartsWith("adb ", StringComparison.OrdinalIgnoreCase))
            {
                cmd = cmd.Substring(4).Trim();
            }

            if (cmd.StartsWith("-s ", StringComparison.OrdinalIgnoreCase))
            {
                AdbCommandPreview = "adb " + cmd;
            }
            else
            {
                AdbCommandPreview = $"adb -s {SelectedDevice.Serial} {cmd}";
            }

            CommandManagerHelper.Invalidate();
        }

        private async Task RebootDeviceAsync()
        {
            if (SelectedDevice == null)
            {
                return;
            }

            var confirm = System.Windows.MessageBox.Show(
                $"确定重启设备 {SelectedDevice.DisplayName} 吗？\n重启后 ADB 连接会断开。",
                "重启设备",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            IsBusy = true;
            StatusMessage = "正在重启设备...";
            try
            {
                _monitorService.StopMonitoring();
                _processMonitorService.StopMonitoring();
                var result = await _adbService.RebootAsync(SelectedDevice.Serial).ConfigureAwait(true);
                AppendAdbTerminalOutput("shell sync; reboot", result);
                Growl.Info("重启命令已发送");
                StatusMessage = "设备正在重启...";
            }
            catch (Exception ex)
            {
                Growl.Error(ex.Message);
                StatusMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ShutdownDeviceAsync()
        {
            if (SelectedDevice == null)
            {
                return;
            }

            var confirm = System.Windows.MessageBox.Show(
                $"确定关闭设备 {SelectedDevice.DisplayName} 吗？\n关机后需手动开机才能重新连接。",
                "关闭设备",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            IsBusy = true;
            StatusMessage = "正在关闭设备...";
            try
            {
                _monitorService.StopMonitoring();
                _processMonitorService.StopMonitoring();
                var result = await _adbService.ShutdownAsync(SelectedDevice.Serial).ConfigureAwait(true);
                AppendAdbTerminalOutput("shell sync; poweroff", result);
                Growl.Info("关机命令已发送");
                StatusMessage = "设备正在关机...";
            }
            catch (Exception ex)
            {
                Growl.Error(ex.Message);
                StatusMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteAdbCommandAsync()
        {
            if (SelectedDevice == null || string.IsNullOrWhiteSpace(AdbTerminalInput))
            {
                return;
            }

            var command = AdbTerminalInput.Trim();
            var preview = AdbCommandPreview;

            IsBusy = true;
            StatusMessage = "正在执行 ADB 命令...";
            try
            {
                var result = await _adbService.RunDeviceCommandAsync(SelectedDevice.Serial, command).ConfigureAwait(true);
                AppendAdbTerminalOutput(preview, result);
                StatusMessage = "ADB 命令执行完成";
            }
            catch (Exception ex)
            {
                AppendAdbTerminalOutput(preview, ex.Message);
                Growl.Error(ex.Message);
                StatusMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task OpenAdbShellAsync()
        {
            if (SelectedDevice == null)
            {
                return;
            }

            try
            {
                var result = await _adbService.OpenInteractiveShellAsync(SelectedDevice.Serial).ConfigureAwait(true);
                AppendAdbTerminalOutput($"adb -s {SelectedDevice.Serial} shell", result);
                Growl.Info("已打开交互式 Shell");
            }
            catch (Exception ex)
            {
                Growl.Error(ex.Message);
            }
        }

        private void AppendAdbTerminalOutput(string commandLine, string output)
        {
            var block = $"[{DateTime.Now:HH:mm:ss}] {commandLine}\r\n{(output ?? string.Empty).Trim()}\r\n\r\n";
            AdbTerminalOutput = block + AdbTerminalOutput;
        }

        private void ClearRemoteFiles()
        {
            RemoteFiles.Clear();
            SelectedRemoteFile = null;
            _selectedRemoteFiles = new List<RemoteFileItem>();
            _remoteFileItemCount = 0;
            CurrentRemotePath = "/";
            RemoteFileSummary = "未连接设备";
            OnPropertyChanged(nameof(SelectedRemoteFileCount));
        }

        public void SetRemoteFileSelection(IEnumerable<RemoteFileItem> items)
        {
            _selectedRemoteFiles = items?.Where(i => i != null).ToList() ?? new List<RemoteFileItem>();
            OnPropertyChanged(nameof(SelectedRemoteFileCount));
            UpdateRemoteFileSummaryText();
            CommandManagerHelper.Invalidate();
        }

        private void UpdateRemoteFileSummaryText()
        {
            var path = RemotePathHelper.Normalize(CurrentRemotePath);
            if (_remoteFileItemCount <= 0)
            {
                RemoteFileSummary = SelectedDevice == null ? "未连接设备" : $"0 项 · {path}";
                return;
            }

            RemoteFileSummary = _selectedRemoteFiles.Count > 0
                ? $"{_remoteFileItemCount} 项 · {path}  ·  已选 {_selectedRemoteFiles.Count} 项"
                : $"{_remoteFileItemCount} 项 · {path}";
        }

        private async Task RefreshRemoteFilesAsync()
        {
            if (SelectedDevice == null)
            {
                return;
            }

            var path = RemotePathHelper.Normalize(CurrentRemotePath);
            CurrentRemotePath = path;

            try
            {
                var listing = await _fileBrowserService.ListDirectoryAsync(SelectedDevice.Serial, path).ConfigureAwait(true);
                RemoteFiles.Clear();
                SelectedRemoteFile = null;
                _selectedRemoteFiles = new List<RemoteFileItem>();
                OnPropertyChanged(nameof(SelectedRemoteFileCount));

                if (!string.IsNullOrWhiteSpace(listing.ErrorMessage))
                {
                    _remoteFileItemCount = 0;
                    RemoteFileSummary = listing.ErrorMessage;
                    Growl.Warning(listing.ErrorMessage);
                    return;
                }

                foreach (var item in listing.Items)
                {
                    RemoteFiles.Add(item);
                }

                _remoteFileItemCount = RemoteFiles.Count;
                UpdateRemoteFileSummaryText();
            }
            catch (Exception ex)
            {
                _remoteFileItemCount = 0;
                RemoteFileSummary = $"读取失败: {ex.Message}";
                Growl.Warning(RemoteFileSummary);
            }
        }

        private async Task GoUpRemoteDirectoryAsync()
        {
            CurrentRemotePath = RemotePathHelper.GetParent(CurrentRemotePath);
            await RefreshRemoteFilesAsync().ConfigureAwait(true);
        }

        private async Task NavigateRemotePathAsync()
        {
            if (SelectedDevice == null)
            {
                return;
            }

            var path = RemotePathHelper.Normalize(CurrentRemotePath);
            if (!await _fileBrowserService.DirectoryExistsAsync(SelectedDevice.Serial, path).ConfigureAwait(true))
            {
                Growl.Warning($"目录不存在: {path}");
                return;
            }

            CurrentRemotePath = path;
            await RefreshRemoteFilesAsync().ConfigureAwait(true);
        }

        private bool CanEnterSelectedRemoteDirectory()
        {
            return SelectedDevice != null
                   && SelectedRemoteFile != null
                   && SelectedRemoteFile.CanEnter
                   && !IsBusy;
        }

        private async Task EnterSelectedRemoteDirectoryAsync()
        {
            if (SelectedRemoteFile == null || !SelectedRemoteFile.CanEnter)
            {
                return;
            }

            CurrentRemotePath = RemotePathHelper.Normalize(SelectedRemoteFile.FullPath);
            await RefreshRemoteFilesAsync().ConfigureAwait(true);
        }

        public async Task HandleRemoteFileDoubleClickAsync()
        {
            if (SelectedDevice == null || SelectedRemoteFile == null || IsBusy)
            {
                return;
            }

            var item = SelectedRemoteFile;
            if (item.IsDirectory)
            {
                await EnterSelectedRemoteDirectoryAsync().ConfigureAwait(true);
                return;
            }

            if (item.IsSymlink
                && await _fileBrowserService.DirectoryExistsAsync(SelectedDevice.Serial, item.FullPath).ConfigureAwait(true))
            {
                await EnterSelectedRemoteDirectoryAsync().ConfigureAwait(true);
                return;
            }

            await ShowRemoteFileActionAsync(item).ConfigureAwait(true);
        }

        private async Task ShowRemoteFileActionAsync(RemoteFileItem item)
        {
            var dialog = new RemoteFileActionDialog(item)
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() != true || !dialog.SelectedAction.HasValue)
            {
                return;
            }

            switch (dialog.SelectedAction.Value)
            {
                case RemoteFileAction.Download:
                    await DownloadRemoteFileAsync(item).ConfigureAwait(true);
                    break;
                case RemoteFileAction.OpenText:
                    await OpenRemoteFileViewerAsync(item, RemoteFileAction.OpenText).ConfigureAwait(true);
                    break;
                case RemoteFileAction.OpenBinary:
                    await OpenRemoteFileViewerAsync(item, RemoteFileAction.OpenBinary).ConfigureAwait(true);
                    break;
            }
        }

        private async Task DownloadRemoteFileAsync(RemoteFileItem item)
        {
            if (SelectedDevice == null)
            {
                return;
            }

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "保存到电脑",
                FileName = DeviceFileBrowserService.GetEntryFileName(item)
            };

            if (dlg.ShowDialog() != true)
            {
                return;
            }

            IsBusy = true;
            StatusMessage = $"正在下载 {item.Name}...";
            try
            {
                var pulled = await _adbService.PullFileAsync(SelectedDevice.Serial, item.FullPath, dlg.FileName)
                    .ConfigureAwait(true);
                if (pulled)
                {
                    Growl.Success($"已保存到 {dlg.FileName}");
                    StatusMessage = $"已下载 {item.Name}";
                }
                else
                {
                    Growl.Warning("下载可能未成功，请检查文件路径与权限");
                    StatusMessage = "下载可能未成功";
                }
            }
            catch (Exception ex)
            {
                Growl.Error(ex.Message);
                StatusMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task OpenRemoteFileViewerAsync(RemoteFileItem item, RemoteFileAction viewMode)
        {
            if (SelectedDevice == null)
            {
                return;
            }

            IsBusy = true;
            StatusMessage = $"正在读取 {item.Name}...";
            try
            {
                var content = await _fileBrowserService.ReadFileBytesAsync(SelectedDevice.Serial, item)
                    .ConfigureAwait(true);

                var viewer = new RemoteFileViewerWindow(item, viewMode)
                {
                    Owner = Application.Current.MainWindow
                };
                viewer.Show();
                viewer.LoadContent(content);

                if (content.IsTruncated)
                {
                    Growl.Info("文件较大，内置查看器仅显示前 2 MB");
                }

                StatusMessage = viewMode == RemoteFileAction.OpenBinary
                    ? $"已打开二进制查看: {item.Name}"
                    : $"已打开记事本: {item.Name}";
            }
            catch (Exception ex)
            {
                Growl.Error(ex.Message);
                StatusMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private IReadOnlyList<RemoteFileItem> GetSelectedRemoteFileItems()
        {
            if (_selectedRemoteFiles.Count > 0)
            {
                return _selectedRemoteFiles;
            }

            return SelectedRemoteFile != null
                ? new[] { SelectedRemoteFile }
                : Array.Empty<RemoteFileItem>();
        }

        private bool CanDeleteSelectedRemoteFile()
        {
            return SelectedDevice != null && !IsBusy && GetSelectedRemoteFileItems().Count > 0;
        }

        private bool CanRenameSelectedRemoteFile()
        {
            if (SelectedDevice == null || IsBusy)
            {
                return false;
            }

            var items = GetSelectedRemoteFileItems();
            return items.Count == 1;
        }

        private async Task CreateRemoteFolderAsync()
        {
            if (SelectedDevice == null)
            {
                return;
            }

            var dialog = new TextInputDialog(
                "新建文件夹",
                $"将在以下目录创建文件夹：\n{CurrentRemotePath}",
                "新建文件夹")
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            IsBusy = true;
            StatusMessage = "正在创建文件夹...";
            try
            {
                await _fileBrowserService.CreateDirectoryAsync(
                    SelectedDevice.Serial,
                    CurrentRemotePath,
                    dialog.InputText).ConfigureAwait(true);
                Growl.Success($"已创建文件夹 {dialog.InputText}");
                StatusMessage = $"已创建文件夹 {dialog.InputText}";
                await RefreshRemoteFilesAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Growl.Error(ex.Message);
                StatusMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task RenameSelectedRemoteFileAsync()
        {
            if (SelectedDevice == null)
            {
                return;
            }

            var items = GetSelectedRemoteFileItems();
            if (items.Count != 1)
            {
                Growl.Warning("请选择一个项目进行重命名");
                return;
            }

            var item = items[0];
            var currentName = DeviceFileBrowserService.GetEntryFileName(item);
            var dialog = new TextInputDialog(
                "重命名",
                $"原名称: {item.Name}\n路径: {item.FullPath}",
                currentName)
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            if (string.Equals(currentName, dialog.InputText, StringComparison.Ordinal))
            {
                return;
            }

            IsBusy = true;
            StatusMessage = $"正在重命名为 {dialog.InputText}...";
            try
            {
                await _fileBrowserService.RenameAsync(SelectedDevice.Serial, item, dialog.InputText)
                    .ConfigureAwait(true);
                Growl.Success($"已重命名为 {dialog.InputText}");
                StatusMessage = $"已重命名为 {dialog.InputText}";
                await RefreshRemoteFilesAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Growl.Error(ex.Message);
                StatusMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task DeleteSelectedRemoteFileAsync()
        {
            if (SelectedDevice == null)
            {
                return;
            }

            var items = GetSelectedRemoteFileItems().ToList();
            if (items.Count == 0)
            {
                return;
            }

            string confirmMessage;
            if (items.Count == 1)
            {
                var item = items[0];
                confirmMessage =
                    $"确定删除以下{item.TypeDisplay}？\n\n名称: {item.Name}\n路径: {item.FullPath}\n\n此操作不可撤销。";
            }
            else
            {
                confirmMessage =
                    $"确定删除选中的 {items.Count} 个项目？\n\n" +
                    string.Join("\n", items.Take(8).Select(i => $"• {i.Name}")) +
                    (items.Count > 8 ? $"\n... 还有 {items.Count - 8} 项" : string.Empty) +
                    "\n\n此操作不可撤销。";
            }

            var confirm = System.Windows.MessageBox.Show(
                confirmMessage,
                items.Count == 1 ? "删除文件" : "批量删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            IsBusy = true;
            StatusMessage = items.Count == 1
                ? $"正在删除 {items[0].Name}..."
                : $"正在删除 {items.Count} 个项目...";
            try
            {
                await _fileBrowserService.DeleteManyAsync(SelectedDevice.Serial, items).ConfigureAwait(true);
                Growl.Success(items.Count == 1
                    ? $"已删除 {items[0].Name}"
                    : $"已删除 {items.Count} 个项目");
                StatusMessage = items.Count == 1
                    ? $"已删除 {items[0].Name}"
                    : $"已删除 {items.Count} 个项目";
                await RefreshRemoteFilesAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Growl.Error(ex.Message);
                StatusMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void BrowseUploadRemoteFiles()
        {
            if (SelectedDevice == null)
            {
                return;
            }

            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择要上传到设备的文件",
                Multiselect = true
            };

            if (dlg.ShowDialog() == true && dlg.FileNames.Length > 0)
            {
                _ = UploadRemoteFilesAsync(dlg.FileNames);
            }
        }

        public async Task UploadRemoteFilesAsync(IEnumerable<string> localPaths)
        {
            if (SelectedDevice == null || localPaths == null)
            {
                return;
            }

            var files = localPaths
                .Where(path => !string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path))
                .ToList();
            if (files.Count == 0)
            {
                Growl.Warning("没有可上传的文件");
                return;
            }

            IsBusy = true;
            StatusMessage = $"正在上传 {files.Count} 个文件到 {CurrentRemotePath}...";
            var successCount = 0;
            var failed = new List<string>();
            try
            {
                foreach (var localPath in files)
                {
                    var fileName = System.IO.Path.GetFileName(localPath);
                    var pushed = await _fileBrowserService.UploadFileAsync(
                        SelectedDevice.Serial,
                        localPath,
                        CurrentRemotePath).ConfigureAwait(true);

                    if (pushed)
                    {
                        successCount++;
                    }
                    else
                    {
                        failed.Add(fileName);
                    }
                }

                await RefreshRemoteFilesAsync().ConfigureAwait(true);

                if (failed.Count == 0)
                {
                    Growl.Success($"已上传 {successCount} 个文件");
                    StatusMessage = $"已上传 {successCount} 个文件到 {CurrentRemotePath}";
                }
                else
                {
                    Growl.Warning($"成功 {successCount} 个，失败 {failed.Count} 个: {string.Join(", ", failed)}");
                    StatusMessage = $"上传完成：成功 {successCount}，失败 {failed.Count}";
                }
            }
            catch (Exception ex)
            {
                Growl.Error(ex.Message);
                StatusMessage = ex.Message;
                await RefreshRemoteFilesAsync().ConfigureAwait(true);
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void Cleanup()
        {
            CancelPartitionTransfer();
            _deviceWatchService.Stop();
            _monitorService.StopMonitoring();
            _processMonitorService.StopMonitoring();
        }

        private bool CanOperateSelectedPartition()
        {
            return SelectedDevice != null
                && SelectedBlockPartition != null
                && !IsBusy
                && _partitionTransferCts == null;
        }

        private bool CanMountSelectedPartition()
        {
            return CanOperateSelectedPartition()
                && !SelectedBlockPartition.IsMounted
                && !PartitionMountService.IsWholeDiskPartition(SelectedBlockPartition);
        }

        private bool CanUnmountSelectedPartition()
        {
            return CanOperateSelectedPartition()
                && SelectedBlockPartition.IsMounted
                && !string.IsNullOrWhiteSpace(SelectedBlockPartition.MountPoint);
        }

        private async Task UpdateSelectedPartitionMountHintsAsync()
        {
            if (SelectedDevice == null || SelectedBlockPartition == null)
            {
                return;
            }

            var partition = SelectedBlockPartition;
            try
            {
                partition.SuggestedMountPoint = await _partitionMountService
                    .ResolveMountPointAsync(SelectedDevice.Serial, partition)
                    .ConfigureAwait(true);

                if (!partition.IsMounted)
                {
                    partition.DetectedFilesystem = await _partitionMountService
                        .DetectFilesystemTypeAsync(SelectedDevice.Serial, partition)
                        .ConfigureAwait(true);
                }
                else
                {
                    partition.DetectedFilesystem = null;
                }

                Application.Current.Dispatcher.Invoke(UpdateSelectedPartitionDetail);
            }
            catch
            {
                partition.SuggestedMountPoint = PartitionMountService.GetSuggestedMountPoint(partition.Name);
                Application.Current.Dispatcher.Invoke(UpdateSelectedPartitionDetail);
            }
        }

        private async Task MountSelectedPartitionAsync()
        {
            if (SelectedDevice == null || SelectedBlockPartition == null)
            {
                return;
            }

            var partition = SelectedBlockPartition;
            if (partition.IsMounted)
            {
                Growl.Info($"分区已挂载于 {partition.MountPoint}");
                return;
            }

            if (PartitionMountService.IsWholeDiskPartition(partition))
            {
                Growl.Warning("不支持挂载整盘设备");
                return;
            }

            IsBusy = true;
            StatusMessage = "正在读取挂载信息...";
            try
            {
                var mountPoint = await _partitionMountService
                    .ResolveMountPointAsync(SelectedDevice.Serial, partition)
                    .ConfigureAwait(true);
                var fsType = await _partitionMountService
                    .DetectFilesystemTypeAsync(SelectedDevice.Serial, partition)
                    .ConfigureAwait(true);

                var mountDialog = new TextInputDialog(
                    "挂载分区",
                    $"分区: {partition.Name}\n" +
                    $"块设备: {partition.ByNamePath ?? partition.BlockDevicePath}\n" +
                    (string.IsNullOrWhiteSpace(fsType) ? "文件系统: 将自动探测\n" : $"检测到文件系统: {fsType}\n") +
                    "\n请输入挂载点:",
                    mountPoint);
                if (mountDialog.ShowDialog() != true)
                {
                    return;
                }

                var readOnly = partition.IsCritical;
                var confirm = System.Windows.MessageBox.Show(
                    $"即将挂载分区 [{partition.Name}]\n\n" +
                    $"块设备: {partition.ByNamePath ?? partition.BlockDevicePath}\n" +
                    $"挂载点: {mountDialog.InputText}\n" +
                    (string.IsNullOrWhiteSpace(fsType) ? "文件系统: 自动探测\n" : $"文件系统: {fsType}\n") +
                    $"挂载选项: {(readOnly ? "只读 (ro)" : "读写 (rw)")}\n\n" +
                    "挂载系统/启动类分区可能影响设备稳定性，请谨慎操作。\n" +
                    "是否继续？",
                    "挂载分区",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes)
                {
                    return;
                }

                StatusMessage = $"正在挂载 {partition.Name}...";
                var result = await _partitionMountService.MountPartitionAsync(
                    SelectedDevice.Serial,
                    partition,
                    mountDialog.InputText,
                    fsType,
                    readOnly).ConfigureAwait(true);

                if (!result.Success)
                {
                    Growl.Error(result.Message ?? "挂载失败");
                    StatusMessage = result.Message ?? "挂载失败";
                    return;
                }

                Growl.Success(result.Message ?? "挂载成功");
                StatusMessage = result.Message ?? "挂载成功";
                await RefreshPartitionsAsync().ConfigureAwait(true);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task UnmountSelectedPartitionAsync()
        {
            if (SelectedDevice == null || SelectedBlockPartition == null || !SelectedBlockPartition.IsMounted)
            {
                return;
            }

            var partition = SelectedBlockPartition;
            var confirm = System.Windows.MessageBox.Show(
                $"即将卸载分区 [{partition.Name}]\n\n" +
                $"挂载点: {partition.MountPoint}\n" +
                $"块设备: {partition.BlockDevicePath}\n\n" +
                "若有程序正在使用该路径，卸载可能失败。\n是否继续？",
                "卸载分区",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            IsBusy = true;
            StatusMessage = $"正在卸载 {partition.Name}...";
            try
            {
                var result = await _partitionMountService
                    .UnmountPartitionAsync(SelectedDevice.Serial, partition)
                    .ConfigureAwait(true);

                if (!result.Success)
                {
                    Growl.Error(result.Message ?? "卸载失败");
                    StatusMessage = result.Message ?? "卸载失败";
                    return;
                }

                Growl.Success(result.Message ?? "卸载成功");
                StatusMessage = result.Message ?? "卸载成功";
                await RefreshPartitionsAsync().ConfigureAwait(true);
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task EnsurePartitionsLoadedAsync()
        {
            if (SelectedDevice == null || SelectedDevice.State != "device" || IsBusy || _allBlockPartitions.Count > 0)
            {
                return;
            }

            await RefreshPartitionsAsync();
        }

        private void ClearPartitions()
        {
            foreach (var partition in _allBlockPartitions)
            {
                partition.PropertyChanged -= PartitionSelectionPropertyChanged;
            }

            _allBlockPartitions.Clear();
            BlockPartitions.Clear();
            SelectedBlockPartition = null;
            _activeAbSlot = null;
            OnPropertyChanged(nameof(ActiveAbSlotDisplay));
            PartitionSummary = "未连接设备（请先选择左侧已连接的设备）";
            SelectedPartitionDetail = "";
            PartitionTransferText = "";
            PartitionTransferPercent = 0;
            OnPropertyChanged(nameof(SelectedPartitionBatchCount));
        }

        private void UpdateSelectedPartitionDetail()
        {
            SelectedPartitionDetail = SelectedBlockPartition == null
                ? "点击上方列表中的分区以查看详情（分区名、目标路径、块设备等）。"
                : SelectedBlockPartition.DetailText;
        }

        private void ApplyPartitionFilterLocal()
        {
            BlockPartitions.Clear();
            foreach (var partition in FilterPartitions(_allBlockPartitions))
            {
                BlockPartitions.Add(partition);
            }

            OnPropertyChanged(nameof(SelectedPartitionBatchCount));
            CommandManagerHelper.Invalidate();
        }

        private IEnumerable<BlockPartitionInfo> FilterPartitions(IEnumerable<BlockPartitionInfo> partitions)
        {
            if (string.IsNullOrWhiteSpace(PartitionSearchText))
            {
                return partitions;
            }

            var query = PartitionSearchText.Trim();
            return partitions.Where(p =>
                (p.Name?.IndexOf(query, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                || (p.BlockDeviceName?.IndexOf(query, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                || (p.ByNamePath?.IndexOf(query, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                || (p.MountPoint?.IndexOf(query, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                || (p.AbSlotLetter?.IndexOf(query, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0);
        }

        private void ToggleSelectAllPartitions()
        {
            var visible = FilterPartitions(_allBlockPartitions).ToList();
            if (visible.Count == 0)
            {
                return;
            }

            var selectAll = visible.Any(p => !p.IsSelectedForBatch);
            foreach (var partition in visible)
            {
                partition.IsSelectedForBatch = selectAll;
            }

            OnPropertyChanged(nameof(SelectedPartitionBatchCount));
            CommandManagerHelper.Invalidate();
        }

        private bool CanBatchExtractPartitions()
        {
            return SelectedDevice != null
                && !IsBusy
                && _partitionTransferCts == null
                && _allBlockPartitions.Any(p => p.IsSelectedForBatch);
        }

        private bool CanBackupPresetPartitions()
        {
            return SelectedDevice != null
                && !IsBusy
                && _partitionTransferCts == null
                && _allBlockPartitions.Count > 0;
        }

        private void PartitionSelectionPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BlockPartitionInfo.IsSelectedForBatch))
            {
                OnPropertyChanged(nameof(SelectedPartitionBatchCount));
                CommandManagerHelper.Invalidate();
            }
        }

        private void AttachPartitionSelectionHandler(BlockPartitionInfo partition)
        {
            if (partition == null)
            {
                return;
            }

            partition.PropertyChanged -= PartitionSelectionPropertyChanged;
            partition.PropertyChanged += PartitionSelectionPropertyChanged;
        }

        private async Task RefreshPartitionsAsync()
        {
            if (SelectedDevice == null)
            {
                return;
            }

            IsBusy = true;
            PartitionSummary = "正在读取分区列表...";
            StatusMessage = "正在读取分区表...";
            try
            {
                var result = await _partitionService.GetPartitionsAsync(SelectedDevice.Serial).ConfigureAwait(true);
                var partitions = result.Partitions?.ToList() ?? new List<BlockPartitionInfo>();
                _activeAbSlot = result.ActiveAbSlot;
                OnPropertyChanged(nameof(ActiveAbSlotDisplay));

                foreach (var partition in _allBlockPartitions)
                {
                    partition.PropertyChanged -= PartitionSelectionPropertyChanged;
                }

                BlockPartitionInfo previous = SelectedBlockPartition;
                _allBlockPartitions = partitions;
                foreach (var partition in _allBlockPartitions)
                {
                    AttachPartitionSelectionHandler(partition);
                    partition.SuggestedMountPoint = PartitionMountService.GetSuggestedMountPoint(partition.Name);
                }

                ApplyPartitionFilterLocal();

                if (previous != null)
                {
                    SelectedBlockPartition = _allBlockPartitions.FirstOrDefault(p =>
                        string.Equals(p.BlockDeviceName, previous.BlockDeviceName, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(p.Name, previous.Name, StringComparison.OrdinalIgnoreCase));
                }

                if (SelectedBlockPartition == null && BlockPartitions.Count > 0)
                {
                    UpdateSelectedPartitionDetail();
                }

                var diskSize = partitions
                    .Where(p => string.Equals(p.BlockDeviceName, "mmcblk0", StringComparison.OrdinalIgnoreCase))
                    .Select(p => p.SizeBytes)
                    .FirstOrDefault();
                if (diskSize <= 0)
                {
                    diskSize = partitions.Where(p => p.SizeBytes > 0).Select(p => p.SizeBytes).DefaultIfEmpty(0).Max();
                }

                var namedCount = partitions.Count(p =>
                    !string.IsNullOrWhiteSpace(p.ByNamePath)
                    && p.ByNamePath.IndexOf("/by-name/", StringComparison.OrdinalIgnoreCase) >= 0);
                var mountedCount = partitions.Count(p => p.IsMounted);

                PartitionSummary = partitions.Count > 0
                    ? $"A/B 当前槽: {ActiveAbSlotDisplay} · 共 {partitions.Count} 项 · 命名 {namedCount} · 已挂载 {mountedCount} · 整盘 {FormatPartitionSize(diskSize)}"
                    : "未找到分区信息";
                StatusMessage = "分区列表已刷新";
            }
            catch (Exception ex)
            {
                Growl.Error($"读取分区失败: {ex.Message}");
                StatusMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExtractSelectedPartitionAsync()
        {
            if (SelectedDevice == null || SelectedBlockPartition == null)
            {
                return;
            }

            var partition = SelectedBlockPartition;
            var defaultPath = PartitionBackupService.BuildDefaultExtractPath(SelectedDevice.Serial, partition.Name);
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "提取分区镜像",
                Filter = "镜像文件|*.img;*.bin|所有文件|*.*",
                FileName = Path.GetFileName(defaultPath),
                InitialDirectory = Path.GetDirectoryName(defaultPath)
            };

            if (dlg.ShowDialog() != true)
            {
                return;
            }

            if (!ConfirmPartitionExtract(new[] { partition }, dlg.FileName, singleFileMode: true))
            {
                return;
            }

            await ExtractPartitionsInternalAsync(
                new[] { partition },
                _ => dlg.FileName,
                "提取分区",
                showPerItemCompletionDialog: true).ConfigureAwait(true);
        }

        private async Task BatchExtractPartitionsAsync()
        {
            var selected = _allBlockPartitions.Where(p => p.IsSelectedForBatch).ToList();
            if (selected.Count == 0 || SelectedDevice == null)
            {
                return;
            }

            var outputDir = PartitionBackupService.BuildBatchBackupDirectory(SelectedDevice.Serial);
            var batchStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var confirm = System.Windows.MessageBox.Show(
                BuildBatchExtractConfirmMessage(selected, outputDir),
                "批量提取分区",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            await ExtractPartitionsInternalAsync(
                selected,
                partition => Path.Combine(
                    outputDir,
                    $"{PartitionBackupService.SanitizeFileName(partition.Name)}_{batchStamp}.img"),
                $"批量提取 ({selected.Count} 项)",
                showPerItemCompletionDialog: false).ConfigureAwait(true);
        }

        private async Task BackupPresetPartitionsAsync()
        {
            if (SelectedDevice == null)
            {
                return;
            }

            var preset = PartitionBackupService.ResolvePresetPartitions(_allBlockPartitions, _activeAbSlot);
            if (preset.Count == 0)
            {
                Growl.Warning("未找到可备份的常用分区（boot/system/trust/userdata）");
                return;
            }

            var outputDir = PartitionBackupService.BuildBatchBackupDirectory(SelectedDevice.Serial);
            var presetNames = string.Join(", ", preset.Select(p => p.Name));
            var confirm = System.Windows.MessageBox.Show(
                BuildBatchExtractConfirmMessage(preset, outputDir, $"常用套装: {presetNames}"),
                "一键备份常用套装",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            await ExtractPartitionsInternalAsync(
                preset,
                partition => Path.Combine(
                    outputDir,
                    $"{PartitionBackupService.SanitizeFileName(partition.Name)}_{stamp}.img"),
                $"常用套装备份 ({preset.Count} 项)",
                showPerItemCompletionDialog: false).ConfigureAwait(true);
        }

        private static string BuildBatchExtractConfirmMessage(
            IReadOnlyList<BlockPartitionInfo> partitions,
            string outputDir,
            string titlePrefix = null)
        {
            var mounted = partitions.Where(p => p.IsMounted).Select(p => p.Name).ToList();
            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(titlePrefix))
            {
                lines.Add(titlePrefix);
                lines.Add("");
            }

            lines.Add($"将提取 {partitions.Count} 个分区到:");
            lines.Add(outputDir);
            lines.Add("");
            lines.Add(string.Join(", ", partitions.Select(p => p.Name)));

            if (mounted.Count > 0)
            {
                lines.Add("");
                lines.Add($"警告: 以下分区当前已挂载，提取可能不一致: {string.Join(", ", mounted)}");
            }

            return string.Join(Environment.NewLine, lines);
        }

        private bool ConfirmPartitionExtract(
            IReadOnlyList<BlockPartitionInfo> partitions,
            string targetPath,
            bool singleFileMode)
        {
            var partition = partitions[0];
            var mountedWarning = partition.IsMounted
                ? $"\n\n警告: 该分区当前已挂载 ({partition.MountPoint})，提取内容可能不一致。"
                : string.Empty;

            var confirm = System.Windows.MessageBox.Show(
                $"将从设备读取分区 [{partition.Name}]\n" +
                $"块设备: {partition.ByNamePath}\n" +
                $"大小: {partition.SizeDisplay}\n" +
                $"A/B 槽: {partition.SlotDisplay}\n\n" +
                $"保存到:\n{targetPath}\n\n" +
                "数据将通过 dd 直接流式传输到电脑，不在设备上暂存。" +
                mountedWarning +
                "\n\n是否继续？",
                singleFileMode ? "提取分区" : "批量提取分区",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            return confirm == MessageBoxResult.Yes;
        }

        private async Task ExtractPartitionsInternalAsync(
            IReadOnlyList<BlockPartitionInfo> partitions,
            Func<BlockPartitionInfo, string> pathSelector,
            string operationTitle,
            bool showPerItemCompletionDialog)
        {
            var summaryLines = new List<string>();
            var failed = new List<string>();

            for (var index = 0; index < partitions.Count; index++)
            {
                var partition = partitions[index];
                var targetPath = pathSelector(partition);
                var itemTitle = $"{operationTitle} [{partition.Name}] ({index + 1}/{partitions.Count})";

                try
                {
                    var savedPath = await RunPartitionTransferAsync(
                        async (progress, token) =>
                        {
                            await _adbService.ExtractBlockDeviceToFileAsync(
                                SelectedDevice.Serial,
                                partition.ByNamePath,
                                targetPath,
                                partition.SizeBytes,
                                progress,
                                token).ConfigureAwait(false);
                            return targetPath;
                        },
                        itemTitle,
                        showCompletionDialog: false).ConfigureAwait(true);

                    summaryLines.Add($"{partition.Name}\n  文件: {savedPath}");

                    if (showPerItemCompletionDialog)
                    {
                        System.Windows.MessageBox.Show(
                            $"{itemTitle} 已完成。\n\n文件: {savedPath}",
                            "完成",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
                catch (OperationCanceledException)
                {
                    Growl.Info($"{itemTitle} 已取消");
                    StatusMessage = $"{itemTitle} 已取消";
                    return;
                }
                catch (Exception ex)
                {
                    failed.Add($"{partition.Name}: {ex.Message}");
                }
            }

            if (partitions.Count > 1 || !showPerItemCompletionDialog)
            {
                var body = summaryLines.Count > 0
                    ? string.Join("\n\n", summaryLines)
                    : "没有成功提取的分区。";
                if (failed.Count > 0)
                {
                    body += "\n\n失败:\n" + string.Join("\n", failed);
                }

                System.Windows.MessageBox.Show(
                    $"{operationTitle} 完成。\n\n{body}",
                    failed.Count > 0 ? "部分完成" : "完成",
                    MessageBoxButton.OK,
                    failed.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }

            if (summaryLines.Count > 0)
            {
                Growl.Success($"{operationTitle} 完成 ({summaryLines.Count}/{partitions.Count})");
            }
        }

        private async Task FlashSelectedPartitionAsync()
        {
            if (SelectedDevice == null || SelectedBlockPartition == null)
            {
                return;
            }

            var partition = SelectedBlockPartition;
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择要刷入的分区镜像",
                Filter = "镜像文件|*.img;*.bin|所有文件|*.*",
                InitialDirectory = PartitionBackupService.GetPartitionBackupDirectory(SelectedDevice.Serial)
            };

            if (dlg.ShowDialog() != true)
            {
                return;
            }

            var fileInfo = new FileInfo(dlg.FileName);
            if (!fileInfo.Exists || fileInfo.Length <= 0)
            {
                Growl.Warning("镜像文件无效");
                return;
            }

            if (partition.SizeBytes > 0 && fileInfo.Length > partition.SizeBytes)
            {
                Growl.Error($"镜像大小 ({FormatPartitionSize(fileInfo.Length)}) 超过分区容量 ({partition.SizeDisplay})，已取消");
                return;
            }

            var mountedWarning = partition.IsMounted
                ? $"警告: 该分区当前已挂载 ({partition.MountPoint})，刷写可能导致文件系统损坏。\n\n"
                : string.Empty;

            var warning = System.Windows.MessageBox.Show(
                $"【高危操作】即将刷写分区 [{partition.Name}]\n\n" +
                $"块设备: {partition.ByNamePath}\n" +
                $"分区大小: {partition.SizeDisplay}\n" +
                $"A/B 槽: {partition.SlotDisplay}\n" +
                $"镜像文件: {dlg.FileName}\n" +
                $"镜像大小: {FormatPartitionSize(fileInfo.Length)}\n\n" +
                mountedWarning +
                "刷写错误镜像可能导致设备无法开机或数据丢失。\n" +
                "数据将通过 dd 从电脑直接写入设备，不在设备上暂存。\n\n" +
                "是否继续？",
                "刷写分区",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (warning != MessageBoxResult.Yes)
            {
                return;
            }

            if (partition.IsCritical)
            {
                var typed = System.Windows.MessageBox.Show(
                    $"[{partition.Name}] 属于关键分区。\n\n" +
                    "请再次确认你完全了解风险。\n" +
                    "若不确定，请立即取消。",
                    "关键分区二次确认",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Stop);
                if (typed != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            await RunPartitionTransferAsync(
                async (progress, token) =>
                {
                    await _adbService.WriteBlockDeviceFromFileAsync(
                        SelectedDevice.Serial,
                        partition.ByNamePath,
                        dlg.FileName,
                        progress,
                        token).ConfigureAwait(false);
                    return dlg.FileName;
                },
                $"刷写 {partition.Name}",
                showCompletionDialog: true).ConfigureAwait(true);
        }

        private async Task<string> RunPartitionTransferAsync(
            Func<IProgress<PartitionIoProgress>, CancellationToken, Task<string>> action,
            string operationName,
            bool showCompletionDialog = true)
        {
            _partitionTransferCts = new CancellationTokenSource();
            OnPropertyChanged(nameof(IsPartitionTransferring));
            CommandManagerHelper.Invalidate();

            IsBusy = true;
            StatusMessage = $"{operationName} 进行中...";
            PartitionTransferText = "准备中...";
            PartitionTransferPercent = 0;

            var progress = new Progress<PartitionIoProgress>(p =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    PartitionTransferText = p.Display;
                    PartitionTransferPercent = p.Percent;
                });
            });

            try
            {
                var resultPath = await action(progress, _partitionTransferCts.Token).ConfigureAwait(true);
                Growl.Success($"{operationName} 完成");
                StatusMessage = $"{operationName} 完成";
                if (showCompletionDialog)
                {
                    System.Windows.MessageBox.Show(
                        $"{operationName} 已完成。\n\n{resultPath}",
                        "完成",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                return resultPath;
            }
            catch (OperationCanceledException)
            {
                Growl.Info($"{operationName} 已取消");
                StatusMessage = $"{operationName} 已取消";
                throw;
            }
            catch (Exception ex)
            {
                Growl.Error($"{operationName} 失败: {ex.Message}");
                StatusMessage = ex.Message;
                throw;
            }
            finally
            {
                _partitionTransferCts?.Dispose();
                _partitionTransferCts = null;
                OnPropertyChanged(nameof(IsPartitionTransferring));
                CommandManagerHelper.Invalidate();
                PartitionTransferText = "";
                PartitionTransferPercent = 0;
                IsBusy = false;
            }
        }

        private void CancelPartitionTransfer()
        {
            _partitionTransferCts?.Cancel();
        }

        private static string FormatPartitionSize(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024)
            {
                return $"{bytes / 1024.0 / 1024.0 / 1024.0:F2} GB";
            }

            if (bytes >= 1024L * 1024)
            {
                return $"{bytes / 1024.0 / 1024.0:F2} MB";
            }

            if (bytes >= 1024)
            {
                return $"{bytes / 1024.0:F1} KB";
            }

            return $"{bytes} B";
        }
    }

    internal static class CommandManagerHelper
    {
        public static void Invalidate() => System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }
}
