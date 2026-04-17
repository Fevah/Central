# Central Platform Architecture

Target: Multi-project modular WPF platform with DevExpress 25.2, Autofac DI,
fine-grained RBAC, and pluggable modules. Based on TotalLink patterns, modernised
for .NET 8 and PostgreSQL (replacing XPO ORM with Npgsql).

---

## 1. Solution Structure

```
Central.sln
│
├── src/
│   ├── Central.Core/                        # Shared framework — NO UI, NO DB
│   │   ├── Auth/
│   │   │   ├── IAuthContext.cs                     # HasPermission("module:action"), observable
│   │   │   ├── AuthContext.cs                      # Singleton — replaces static UserSession
│   │   │   ├── PermissionCode.cs                   # String constants: P.DevicesRead, P.SwitchesDeploy
│   │   │   ├── PermissionGuard.cs                  # Guard.Require(P.DevicesWrite) — throws if denied
│   │   │   └── AuthStates.cs                       # NotAuthenticated, Windows, Offline
│   │   ├── Data/
│   │   │   ├── IRepository.cs                      # Generic async CRUD interface
│   │   │   ├── IUnitOfWork.cs                      # Transaction scope (replaces XPO UnitOfWork)
│   │   │   └── ConnectionManager.cs                # Offline mode, 5s timeout, 10s retry, events
│   │   ├── Modules/
│   │   │   ├── IModule.cs                          # RegisterServices(ContainerBuilder)
│   │   │   ├── IModuleRibbon.cs                    # RegisterRibbon(IRibbonBuilder)
│   │   │   ├── IModulePanels.cs                    # RegisterPanels(IPanelBuilder)
│   │   │   └── ModuleLoader.cs                     # AppDomain.GetAssemblies() scan
│   │   ├── Models/
│   │   │   ├── EntityBase.cs                       # Id, CreatedAt, UpdatedAt, IsDeleted
│   │   │   ├── IAuditable.cs                       # Audit trail marker
│   │   │   └── INetworkLink.cs                     # P2P/B2B/FW shared interface
│   │   ├── Commands/
│   │   │   ├── AsyncRelayCommand.cs                # ICommand with async + IsExecuting flag
│   │   │   └── AsyncRelayCommand{T}.cs             # Generic typed version
│   │   ├── ViewModels/
│   │   │   ├── ViewModelBase.cs                    # INotifyPropertyChanged + SetProperty
│   │   │   └── EntityViewModelBase{T}.cs           # Wraps EntityBase, auto-syncs collections
│   │   ├── Widgets/
│   │   │   ├── WidgetViewModelBase.cs              # Base for all panel content
│   │   │   ├── WidgetCommandAttribute.cs           # [WidgetCommand("Add {Type}", "Edit")]
│   │   │   ├── WidgetCommandData.cs                # Text replacement: {Type} → "Device"
│   │   │   └── IWidgetEvents.cs                    # WidgetLoaded, WidgetStarted, WidgetClosed
│   │   ├── Services/
│   │   │   ├── IAuditService.cs                    # Append-only audit log
│   │   │   ├── INotificationService.cs             # Toast/status bar
│   │   │   ├── ILayoutService.cs                   # Save/restore per-user
│   │   │   └── IDetailDialogService.cs             # ShowDialog(EditMode, entity)
│   │   └── Extensions/
│   │       ├── NaturalSortExtensions.cs
│   │       └── StringExtensions.cs
│   │
│   ├── Central.Data/                         # PostgreSQL data access
│   │   ├── NpgsqlUnitOfWork.cs                     # IUnitOfWork → Npgsql transaction
│   │   ├── RepositoryBase.cs                       # SafeWriteAsync, query helpers
│   │   ├── Repositories/
│   │   │   ├── DeviceRepository.cs
│   │   │   ├── SwitchRepository.cs
│   │   │   ├── LinkRepository.cs                   # P2P + B2B + FW
│   │   │   ├── BgpRepository.cs
│   │   │   ├── VlanRepository.cs
│   │   │   ├── UserRepository.cs
│   │   │   ├── RoleRepository.cs
│   │   │   ├── PermissionRepository.cs
│   │   │   ├── AuditRepository.cs
│   │   │   └── SettingsRepository.cs
│   │   ├── Migrations/
│   │   │   └── MigrationRunner.cs                  # Auto-apply numbered .sql files
│   │   └── DataModule.cs                           # Autofac module — registers all repos
│   │
│   ├── Central.Shell/                        # WPF host — thin shell
│   │   ├── App.xaml.cs                             # Bootstrapper.RunStartup()
│   │   ├── Bootstrapper.cs                         # DI build, module scan, auth, splash
│   │   ├── MainWindow.xaml                         # DXRibbonWindow + ContentControl
│   │   ├── MainWindow.xaml.cs                      # Window state binding only
│   │   ├── ViewModels/
│   │   │   ├── ShellViewModel.cs                   # RibbonCategories, Documents, StatusBar
│   │   │   ├── MainViewModel.cs                    # Ribbon orchestration, document host
│   │   │   └── StatusBarViewModel.cs               # Connection, user, notifications
│   │   ├── Services/
│   │   │   ├── RibbonBuilder.cs                    # Fluent API from module registrations
│   │   │   ├── PanelBuilder.cs                     # Panel registration with permission gates
│   │   │   ├── DetailDialogService.cs              # Dialog creation + window state
│   │   │   ├── ThemeService.cs                     # ThemeManager + gallery
│   │   │   └── AutofacViewLocator.cs               # IViewLocator — resolves View by ViewModel name
│   │   ├── Startup/
│   │   │   ├── StartupWorkerManager.cs             # Sequential worker orchestration + progress
│   │   │   ├── StartupWorkerBase.cs                # BackgroundWorker with Steps + progress
│   │   │   ├── InitModulesStartupWorker.cs         # Scan assemblies, register Autofac modules
│   │   │   └── LoginStartupWorker.cs               # Windows auth / offline fallback
│   │   ├── Converters/
│   │   │   ├── NullToBoolConverter.cs
│   │   │   ├── StringToBrushConverter.cs
│   │   │   └── PermissionToVisibilityConverter.cs
│   │   └── TemplateSelectors/
│   │       ├── RibbonCategoryTemplateSelector.cs   # Routes ViewModel type → XAML DataTemplate
│   │       ├── RibbonPageTemplateSelector.cs
│   │       └── BackstageItemTemplateSelector.cs
│   │
│   ├── Central.Module.Devices/               # IPAM module
│   │   ├── DevicesModule.cs                        # : Autofac.Module, IModule, IModuleRibbon, IModulePanels
│   │   ├── Views/
│   │   │   ├── DeviceGridPanel.xaml                # GridEdit control + GridEditStrategy
│   │   │   └── DeviceDetailPanel.xaml
│   │   ├── ViewModels/
│   │   │   ├── DeviceListViewModel.cs              # : ListViewModelBase<DeviceRecord>
│   │   │   └── DeviceDetailViewModel.cs
│   │   └── Services/
│   │       └── DeviceService.cs
│   │
│   ├── Central.Module.Switches/
│   │   ├── SwitchesModule.cs
│   │   ├── Views/
│   │   │   ├── SwitchGridPanel.xaml
│   │   │   ├── SwitchDetailPanel.xaml
│   │   │   └── RunningConfigPanel.xaml
│   │   ├── ViewModels/
│   │   │   ├── SwitchListViewModel.cs
│   │   │   └── ConfigCompareViewModel.cs
│   │   └── Services/
│   │       ├── PingService.cs
│   │       └── SshService.cs
│   │
│   ├── Central.Module.Links/
│   │   ├── LinksModule.cs
│   │   ├── Views/
│   │   │   ├── P2PGridPanel.xaml
│   │   │   ├── B2BGridPanel.xaml
│   │   │   └── FWGridPanel.xaml
│   │   ├── ViewModels/
│   │   │   ├── P2PListViewModel.cs                 # : ListViewModelBase<P2PLink>
│   │   │   ├── B2BListViewModel.cs
│   │   │   └── FWListViewModel.cs
│   │   └── Services/
│   │       ├── LinkEditorHelper.cs
│   │       └── ConfigBuilderService.cs
│   │
│   ├── Central.Module.Routing/
│   │   ├── RoutingModule.cs
│   │   ├── Views/
│   │   │   ├── BgpGridPanel.xaml
│   │   │   └── BgpDetailPanel.xaml
│   │   ├── ViewModels/
│   │   │   └── BgpListViewModel.cs
│   │   └── Services/
│   │       └── BgpSyncService.cs
│   │
│   ├── Central.Module.VLANs/
│   │   ├── VlansModule.cs
│   │   ├── Views/
│   │   │   └── VlanGridPanel.xaml
│   │   ├── ViewModels/
│   │   │   └── VlanListViewModel.cs
│   │   └── Services/
│   │       └── VlanService.cs
│   │
│   └── Central.Module.Admin/
│       ├── AdminModule.cs
│       ├── Views/
│       │   ├── UserGridPanel.xaml
│       │   ├── RoleGridPanel.xaml
│       │   ├── PermissionTreePanel.xaml
│       │   ├── LookupGridPanel.xaml
│       │   └── AuditLogPanel.xaml
│       ├── ViewModels/
│       │   ├── UserListViewModel.cs
│       │   ├── RoleListViewModel.cs
│       │   └── AuditLogViewModel.cs
│       └── Services/
│           └── RbacService.cs
│
├── db/
│   ├── schema.sql
│   └── migrations/
│       ├── 001–023 (existing)
│       ├── 024_permissions_v2.sql
│       └── 025_audit_log_v2.sql
│
└── tests/
    ├── Central.Engine.Tests/
    ├── Central.Persistence.Tests/
    └── Central.Module.Links.Tests/
```

---

## 2. Bootstrap Sequence

Based on TotalLink's `Bootstrapper.cs` → `BootstrapperBase.cs` → `StartupWorkerManager`:

```csharp
// Central.Shell/App.xaml.cs
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    var bootstrapper = new Bootstrapper();
    bootstrapper.RunStartup();
}

// Central.Shell/Bootstrapper.cs
public class Bootstrapper
{
    public void RunStartup()
    {
        // 1. Show splash screen
        DXSplashScreen.Show<SplashScreenView>();

        // 2. Apply saved theme
        ThemeManager.ApplicationThemeName = Settings.Default.ThemeName;

        // 3. Build startup worker pipeline
        var manager = new StartupWorkerManager();
        manager.Enqueue(new InitModulesStartupWorker());   // Scan + register modules
        manager.Enqueue(new LoginStartupWorker());          // Windows auth / offline
        manager.Completed += OnStartupCompleted;
        manager.Run();
    }

    private void OnStartupCompleted()
    {
        DXSplashScreen.Close();

        if (AuthContext.Instance.AuthState == AuthStates.NotAuthenticated)
        {
            ShowLoginWindow();
            return;
        }

        ShowMainWindow();
    }

    private void ShowMainWindow()
    {
        var shell = Container.Resolve<ShellViewModel>();

        // Each module registers its ribbon + panels
        foreach (var mod in _modules.OfType<IModuleRibbon>().OrderBy(m => m.SortOrder))
            mod.RegisterRibbon(shell.RibbonBuilder);
        foreach (var mod in _modules.OfType<IModulePanels>())
            mod.RegisterPanels(shell.PanelBuilder);

        var window = Container.Resolve<MainWindow>();
        window.DataContext = shell;
        window.Show();
    }
}
```

### InitModulesStartupWorker (from TotalLink pattern)

```csharp
// Runs on background thread during splash screen
public class InitModulesStartupWorker : StartupWorkerBase
{
    protected override void OnDoWork()
    {
        var builder = new ContainerBuilder();

        // Core services (always registered)
        builder.RegisterType<AuthContext>().As<IAuthContext>().SingleInstance();
        builder.RegisterType<ConnectionManager>().AsSelf().SingleInstance();
        builder.RegisterType<AuditService>().As<IAuditService>().SingleInstance();
        builder.RegisterType<LayoutService>().As<ILayoutService>().SingleInstance();
        builder.RegisterType<NotificationService>().As<INotificationService>().SingleInstance();
        builder.RegisterModule<DataModule>();  // All repositories

        // Shell views + viewmodels
        builder.RegisterType<MainWindow>().SingleInstance();
        builder.RegisterType<ShellViewModel>().SingleInstance();
        builder.RegisterType<MainViewModel>().SingleInstance();

        ReportProgress(0, "Discovering modules...");

        // Scan assemblies for IModule implementations
        var modules = new List<IModule>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var type in assembly.GetTypes()
                .Where(t => typeof(IModule).IsAssignableFrom(t) && !t.IsAbstract))
            {
                var module = (IModule)Activator.CreateInstance(type);
                modules.Add(module);
                builder.RegisterModule((Autofac.Module)module);
            }
        }

        ReportProgress(1, "Building container...");
        Container = builder.Build();
        ViewLocator.Default = new AutofacViewLocator(Container);
    }
}
```

---

## 3. Module Interface

Direct adaptation of TotalLink's Autofac module pattern:

```csharp
// Central.Core/Modules/IModule.cs
public interface IModule
{
    string Name { get; }                // "Devices", "Links", "Routing"
    string PermissionCategory { get; }  // "devices", "links", "bgp"
    int SortOrder { get; }              // Ribbon tab ordering (10, 20, 30...)
}

// Central.Core/Modules/IModuleRibbon.cs
public interface IModuleRibbon
{
    void RegisterRibbon(IRibbonBuilder ribbon);
}

// Central.Core/Modules/IModulePanels.cs
public interface IModulePanels
{
    void RegisterPanels(IPanelBuilder panels);
}
```

### Example Module (Links)

Follows TotalLink's `AdminModule.cs` pattern — Autofac.Module + IModule:

```csharp
// Central.Module.Links/LinksModule.cs
public class LinksModule : Autofac.Module, IModule, IModuleRibbon, IModulePanels
{
    public string Name => "Links";
    public string PermissionCategory => "links";
    public int SortOrder => 30;

    protected override void Load(ContainerBuilder builder)
    {
        // Services — SingleInstance (like TotalLink facades)
        builder.RegisterType<LinkEditorHelper>().AsSelf().SingleInstance();
        builder.RegisterType<ConfigBuilderService>().AsSelf().SingleInstance();

        // ViewModels — InstancePerDependency (new per panel open)
        builder.RegisterType<P2PListViewModel>().AsSelf();
        builder.RegisterType<B2BListViewModel>().AsSelf();
        builder.RegisterType<FWListViewModel>().AsSelf();

        // Views
        builder.RegisterType<P2PGridPanel>().AsSelf();
        builder.RegisterType<B2BGridPanel>().AsSelf();
        builder.RegisterType<FWGridPanel>().AsSelf();
    }

    public void RegisterRibbon(IRibbonBuilder ribbon)
    {
        ribbon.AddPage("Links", SortOrder, page =>
        {
            page.AddGroup("Actions", group =>
            {
                group.AddButton("New Link",    P.LinksWrite,  "New_32x32",    vm => vm.AddCommand);
                group.AddButton("Delete Link", P.LinksDelete, "Delete_32x32", vm => vm.DeleteCommand);
            });
            page.AddGroup("Panels", group =>
            {
                group.AddCheckButton("P2P",  panelId: "P2PPanel");
                group.AddCheckButton("B2B",  panelId: "B2BPanel");
                group.AddCheckButton("FW",   panelId: "FWPanel");
            });
            page.AddGroup("Config", group =>
            {
                group.AddButton("Build Config", P.LinksRead, "Preview_32x32",
                    vm => vm.BuildConfigCommand);
            });
        });
    }

    public void RegisterPanels(IPanelBuilder panels)
    {
        panels.AddPanel<P2PGridPanel, P2PListViewModel>(
            "P2PPanel", "P2P Links", P.LinksRead, DockPosition.Document);
        panels.AddPanel<B2BGridPanel, B2BListViewModel>(
            "B2BPanel", "B2B Links", P.LinksRead, DockPosition.Document);
        panels.AddPanel<FWGridPanel, FWListViewModel>(
            "FWPanel", "FW Links", P.LinksRead, DockPosition.Document);
    }
}
```

---

## 4. ListViewModelBase (Grid CRUD)

Based on TotalLink's `ListViewModelBase<TEntity>` (815 lines). Adapted for Npgsql
instead of XPO, keeping the same command pattern and [WidgetCommand] attributes:

```csharp
// Central.Core/ViewModels/ListViewModelBase.cs
public abstract class ListViewModelBase<T> : WidgetViewModelBase where T : EntityBase, new()
{
    protected readonly IRepository<T> Repo;
    protected readonly IAuthContext Auth;
    protected readonly IAuditService Audit;
    protected readonly INotificationService Notify;

    public ObservableCollection<T> Items { get; } = new();
    public ObservableCollection<T> SelectedItems { get; } = new();
    public T? CurrentItem { get; set; }

    // ── Permission-gated properties ──
    public bool CanAdd    => Auth.HasPermission($"{Category}:write");
    public bool CanEdit   => Auth.HasPermission($"{Category}:write");
    public bool CanDelete => Auth.HasPermission($"{Category}:delete");

    protected abstract string Category { get; }  // "links", "devices", "bgp"

    // ── Auto-generated ribbon buttons via [WidgetCommand] ──

    [WidgetCommand("Add {Type}", "Edit", RibbonItemType.ButtonItem)]
    public AsyncRelayCommand AddCommand { get; }

    [WidgetCommand("Delete {TypePlural}", "Edit", RibbonItemType.ButtonItem)]
    public AsyncRelayCommand DeleteCommand { get; }

    [WidgetCommand("Refresh {TypePlural}", "Edit", RibbonItemType.ButtonItem)]
    public AsyncRelayCommand RefreshCommand { get; }

    // Text replacements: {Type} → "P2P Link", {TypePlural} → "P2P Links"
    protected virtual string TypeName => typeof(T).Name;
    protected virtual string TypeNamePlural => TypeName + "s";

    // ── CRUD implementation ──

    protected virtual async Task OnAddExecuteAsync()
    {
        if (UseAddDialog)
        {
            var item = new T();
            if (DetailDialogService.ShowDialog(DetailEditMode.Add, item, TypeName))
            {
                await Repo.InsertAsync(item);
                await Audit.LogAsync(Category, item.Id, "create", null, item);
                Items.Add(item);
                Notify.Success($"{TypeName} created");
            }
        }
        else
        {
            // Inline — new row at top
            var item = new T();
            Items.Insert(0, item);
        }
    }

    protected virtual async Task OnDeleteExecuteAsync()
    {
        if (SelectedItems.Count == 0) return;

        var msg = SelectedItems.Count == 1
            ? $"Delete this {TypeName}?"
            : $"Delete {SelectedItems.Count} {TypeNamePlural}?";

        if (MessageBoxService.Show(msg, "Confirm", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            return;

        foreach (var item in SelectedItems.ToList())
        {
            item.IsDeleted = true;
            item.DeletedAt = DateTime.UtcNow;
            await Repo.UpdateAsync(item);
            await Audit.LogAsync(Category, item.Id, "delete", item, null);
            Items.Remove(item);
        }

        Notify.Success($"{SelectedItems.Count} {TypeNamePlural} deleted");
    }

    protected virtual async Task OnRefreshExecuteAsync()
    {
        Items.Clear();
        var data = await Repo.GetAllAsync();
        foreach (var item in data) Items.Add(item);
    }

    // ── Row validation (inline edit auto-save) ──

    public async Task OnRowValidatedAsync(T item)
    {
        try
        {
            if (item.Id == 0)
            {
                await Repo.InsertAsync(item);
                await Audit.LogAsync(Category, item.Id, "create", null, item);
            }
            else
            {
                var old = await Repo.GetByIdAsync(item.Id);
                await Repo.UpdateAsync(item);
                await Audit.LogAsync(Category, item.Id, "update", old, item);
            }
            Notify.Success($"{TypeName} saved");
        }
        catch (Exception ex)
        {
            Notify.Error($"Save failed: {ex.Message}");
        }
    }

    // ── Selection broadcasting (TotalLink Messenger pattern) ──

    protected override void OnSelectedItemsChanged()
    {
        Messenger.Default.Send(new SelectedItemsChangedMessage(this, SelectedItems));
    }

    // ── Dialog vs inline toggle ──
    protected virtual bool UseAddDialog => false;  // Override per module
}
```

---

## 5. WidgetCommand Auto-Ribbon

TotalLink's most elegant pattern — ribbon buttons auto-generated from attributes:

```csharp
// Central.Core/Widgets/WidgetCommandAttribute.cs
[AttributeUsage(AttributeTargets.Property)]
public class WidgetCommandAttribute : Attribute
{
    public string Name { get; }           // "Add {Type}" → "Add P2P Link"
    public string GroupName { get; }      // "Edit" — ribbon group
    public RibbonItemType ItemType { get; }
    public string Description { get; }    // Tooltip
    public string Permission { get; set; } // P.LinksWrite — gates visibility

    public WidgetCommandAttribute(string name, string groupName,
        RibbonItemType itemType, string description = "")
    {
        Name = name;
        GroupName = groupName;
        ItemType = itemType;
        Description = description;
    }
}

// Text replacement (from TotalLink's WidgetCommandData.cs)
// When ListViewModelBase<P2PLink> is active:
//   {Type}       → "P2P Link"
//   {TypePlural} → "P2P Links"
```

**How it works at runtime:**

When a panel gets focus, the shell reads its WidgetViewModel's `[WidgetCommand]`
attributes and builds `RibbonGroupViewModel` → `RibbonItemViewModel` objects.
The ribbon tab dynamically updates to show that panel's commands.

```csharp
// Central.Shell/Services/RibbonBuilder.cs
public void RefreshWidgetRibbon(WidgetViewModelBase activeWidget)
{
    _contextRibbonGroups.Clear();

    var commands = activeWidget.GetType().GetProperties()
        .Where(p => p.GetCustomAttribute<WidgetCommandAttribute>() != null)
        .Select(p => new {
            Attr = p.GetCustomAttribute<WidgetCommandAttribute>(),
            Command = p.GetValue(activeWidget) as ICommand
        })
        .Where(x => string.IsNullOrEmpty(x.Attr.Permission)
                  || Auth.HasPermission(x.Attr.Permission));

    foreach (var group in commands.GroupBy(c => c.Attr.GroupName))
    {
        var ribbonGroup = new RibbonGroupViewModel(group.Key);
        foreach (var cmd in group)
        {
            ribbonGroup.Items.Add(new RibbonItemViewModel
            {
                Content = activeWidget.ReplaceText(cmd.Attr.Name),
                Command = cmd.Command,
                Description = activeWidget.ReplaceText(cmd.Attr.Description),
                ItemType = cmd.Attr.ItemType
            });
        }
        _contextRibbonGroups.Add(ribbonGroup);
    }
}
```

---

## 6. Dynamic Ribbon from ViewModel Binding

Based on TotalLink's `DocumentManagerView.xaml.cs` — ribbon is an empty container,
categories bound from ViewModel, templates resolve UI:

```csharp
// Central.Shell/MainWindow.xaml
<dxr:DXRibbonWindow>
    <dxr:DXRibbonWindow.Resources>
        <!-- DataTemplates for each ribbon ViewModel type -->
        <DataTemplate DataType="{x:Type vm:RibbonCategoryViewModel}">
            <dxr:RibbonPageCategory Caption="{Binding Caption}" />
        </DataTemplate>
        <DataTemplate DataType="{x:Type vm:RibbonPageViewModel}">
            <dxr:RibbonPage Caption="{Binding Caption}" />
        </DataTemplate>
        <!-- ... -->
    </dxr:DXRibbonWindow.Resources>

    <dxr:RibbonControl x:Name="Ribbon"
        CategoriesSource="{Binding RibbonCategories}"
        CategoryTemplateSelector="{StaticResource RibbonCategoryTemplateSelector}" />

    <ContentControl Content="{Binding ActiveDocument}" />
</dxr:DXRibbonWindow>

// Template selector routes ViewModel type → DataTemplate
public class RibbonCategoryTemplateSelector : DataTemplateSelector
{
    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        var ribbonControl = container as RibbonControl;
        return (DataTemplate)ribbonControl.TryFindResource(new DataTemplateKey(item.GetType()));
    }
}
```

---

## 7. Permission System (module:action)

### Database Schema

```sql
-- 024_permissions_v2.sql

CREATE TABLE IF NOT EXISTS permissions (
    id          SERIAL PRIMARY KEY,
    code        VARCHAR(100) NOT NULL UNIQUE,   -- 'devices:read'
    name        VARCHAR(255) NOT NULL,          -- 'View Devices'
    category    VARCHAR(64) NOT NULL,           -- 'devices'
    description TEXT,
    is_system   BOOLEAN NOT NULL DEFAULT TRUE,
    sort_order  INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS role_permission_grants (
    role_id         INTEGER NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
    permission_id   INTEGER NOT NULL REFERENCES permissions(id) ON DELETE CASCADE,
    PRIMARY KEY (role_id, permission_id)
);

-- Seed permissions
INSERT INTO permissions (code, name, category, sort_order) VALUES
    -- Devices / IPAM
    ('devices:read',     'View Devices',         'devices',  10),
    ('devices:write',    'Edit Devices',         'devices',  20),
    ('devices:delete',   'Delete Devices',       'devices',  30),
    ('devices:export',   'Export Devices',       'devices',  40),
    ('devices:reserved', 'View Reserved',        'devices',  50),
    -- Switches
    ('switches:read',    'View Switches',        'switches', 10),
    ('switches:write',   'Edit Switches',        'switches', 20),
    ('switches:delete',  'Delete Switches',      'switches', 30),
    ('switches:ping',    'Ping Switches',        'switches', 40),
    ('switches:ssh',     'SSH to Switches',      'switches', 50),
    ('switches:sync',    'Sync Running Config',  'switches', 60),
    ('switches:deploy',  'Deploy Config',        'switches', 70),
    -- Links
    ('links:read',       'View Links',           'links',    10),
    ('links:write',      'Edit Links',           'links',    20),
    ('links:delete',     'Delete Links',         'links',    30),
    -- Routing / BGP
    ('bgp:read',         'View BGP',             'bgp',      10),
    ('bgp:write',        'Edit BGP',             'bgp',      20),
    ('bgp:sync',         'Sync BGP from Switch', 'bgp',      30),
    -- VLANs
    ('vlans:read',       'View VLANs',           'vlans',    10),
    ('vlans:write',      'Edit VLANs',           'vlans',    20),
    -- Admin
    ('admin:users',      'Manage Users',         'admin',    10),
    ('admin:roles',      'Manage Roles',         'admin',    20),
    ('admin:lookups',    'Manage Lookups',       'admin',    30),
    ('admin:settings',   'Manage Settings',      'admin',    40),
    ('admin:audit',      'View Audit Log',       'admin',    50)
ON CONFLICT (code) DO NOTHING;
```

### AuthContext (replaces UserSession)

```csharp
// Central.Core/Auth/AuthContext.cs
public class AuthContext : ViewModelBase, IAuthContext
{
    private static AuthContext? _instance;
    public static AuthContext Instance => _instance ??= new();

    private AppUser? _currentUser;
    private HashSet<string> _permissions = new();
    private HashSet<string> _allowedSites = new();
    private AuthStates _authState = AuthStates.NotAuthenticated;

    public AppUser? CurrentUser { get => _currentUser; private set => SetProperty(ref _currentUser, value); }
    public AuthStates AuthState { get => _authState; set => SetProperty(ref _authState, value); }
    public IReadOnlySet<string> AllowedSites => _allowedSites;
    public bool IsAuthenticated => AuthState != AuthStates.NotAuthenticated;
    public bool IsSuperAdmin => CurrentUser?.Priority >= 1000;

    public bool HasPermission(string code)
    {
        if (IsSuperAdmin) return true;
        return _permissions.Contains(code);
    }

    public bool HasAnyPermission(params string[] codes)
        => codes.Any(HasPermission);

    public bool HasSiteAccess(string building)
    {
        if (IsSuperAdmin) return true;
        if (_allowedSites.Count == 0) return true;  // No restrictions = all sites
        return _allowedSites.Contains(building);
    }

    public async Task LoginAsync(string username, IUserRepository userRepo,
        IPermissionRepository permRepo)
    {
        CurrentUser = await userRepo.GetByUsernameAsync(username);
        if (CurrentUser == null)
        {
            AuthState = AuthStates.Offline;
            return;
        }

        var perms = await permRepo.GetPermissionCodesForRoleAsync(CurrentUser.RoleId);
        _permissions = new HashSet<string>(perms);

        var sites = await permRepo.GetAllowedSitesAsync(CurrentUser.RoleId);
        _allowedSites = new HashSet<string>(sites);

        AuthState = AuthStates.Windows;
        OnPropertyChanged(nameof(IsAuthenticated));
        PermissionsChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler<EventArgs>? PermissionsChanged;
}
```

### Permission Constants

```csharp
// Central.Core/Auth/PermissionCode.cs
public static class P
{
    public const string DevicesRead     = "devices:read";
    public const string DevicesWrite    = "devices:write";
    public const string DevicesDelete   = "devices:delete";
    public const string DevicesExport   = "devices:export";
    public const string DevicesReserved = "devices:reserved";

    public const string SwitchesRead    = "switches:read";
    public const string SwitchesWrite   = "switches:write";
    public const string SwitchesDelete  = "switches:delete";
    public const string SwitchesPing    = "switches:ping";
    public const string SwitchesSsh     = "switches:ssh";
    public const string SwitchesSync    = "switches:sync";
    public const string SwitchesDeploy  = "switches:deploy";

    public const string LinksRead       = "links:read";
    public const string LinksWrite      = "links:write";
    public const string LinksDelete     = "links:delete";

    public const string BgpRead         = "bgp:read";
    public const string BgpWrite        = "bgp:write";
    public const string BgpSync         = "bgp:sync";

    public const string VlansRead       = "vlans:read";
    public const string VlansWrite      = "vlans:write";

    public const string AdminUsers      = "admin:users";
    public const string AdminRoles      = "admin:roles";
    public const string AdminLookups    = "admin:lookups";
    public const string AdminSettings   = "admin:settings";
    public const string AdminAudit      = "admin:audit";
}
```

---

## 8. Panel/Document/Widget Composition

Based on TotalLink's `DocumentViewModel` → `PanelGroupViewModel` → `PanelViewModel`
→ `WidgetViewModelBase` hierarchy:

```
ShellViewModel
├── RibbonCategories (bound to RibbonControl.CategoriesSource)
├── Documents (ObservableCollection<DocumentViewModel>)
│   └── DocumentViewModel (container for panels)
│       ├── PanelGroups (logical groups — horizontal/vertical splits)
│       │   └── PanelGroupViewModel
│       │       └── Panels (ObservableCollection<PanelViewModel>)
│       └── FlatPanels (all panels regardless of group)
└── StatusBar (connection, user, notifications)

PanelViewModel
├── Id, Caption, Permission
├── View (FrameworkElement — resolved via ViewLocator)
├── WidgetViewModel (WidgetViewModelBase — resolved via Autofac)
├── IsCustomization (toggle edit mode)
└── KeyedData (grid layouts, widget state — persisted per user)
```

**Panel lifecycle:**
1. Module registers panel type via `IPanelBuilder.AddPanel<TView, TViewModel>(...)`
2. Shell creates `PanelViewModel` on demand (ribbon toggle or startup)
3. `ViewLocator.Resolve<TView>()` creates the UserControl
4. Autofac resolves `TViewModel` and assigns as DataContext
5. Widget fires `WidgetLoaded` → loads data
6. Panel state saved on close via `ILayoutService`

---

## 9. Detail Dialog Pattern

From TotalLink's `DetailDialogService.cs` + `DetailDialogViewModel.cs`:

```csharp
// Central.Shell/Services/DetailDialogService.cs
public class DetailDialogService : IDetailDialogService
{
    public bool ShowDialog(DetailEditMode editMode, INotifyPropertyChanged editObject,
        string? typeName = null)
    {
        var vm = new DetailDialogViewModel(editMode, editObject);
        var viewName = editObject.GetType().Name + "DetailView";
        var view = ViewLocator.Default.ResolveView(viewName);

        var window = new DXWindow
        {
            Content = view,
            DataContext = vm,
            Title = $"{editMode} {typeName ?? editObject.GetType().Name}",
            Owner = Application.Current.MainWindow,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SizeToContent = SizeToContent.WidthAndHeight
        };

        // Restore window state from last use
        var stateKey = $"DetailDialog_{editObject.GetType().Name}";
        LayoutService.RestoreWindowState(window, stateKey);

        vm.OkCommand = new RelayCommand(
            () => { window.DialogResult = true; },
            () => (vm.IsModified || editMode == DetailEditMode.Add) && vm.IsValid);

        var result = window.ShowDialog() == true;
        LayoutService.SaveWindowState(window, stateKey);
        return result;
    }
}
```

---

## 10. Change Tracking and Undo/Redo

TotalLink uses MonitoredUndo with custom `ChangeFactoryEx`. For Central,
a lighter approach using property snapshots:

```csharp
// Central.Core/Models/EntityBase.cs
public abstract class EntityBase : ViewModelBase
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Snapshot for audit diff
    public Dictionary<string, object?> TakeSnapshot()
    {
        return GetType().GetProperties()
            .Where(p => p.CanRead && p.Name != nameof(TakeSnapshot))
            .ToDictionary(p => p.Name, p => p.GetValue(this));
    }
}
```

Future Phase 3: integrate MonitoredUndo NuGet for full undo/redo stacks in ribbon.

---

## 11. Audit Log

```sql
-- 025_audit_log_v2.sql
CREATE TABLE IF NOT EXISTS audit_log (
    id          BIGSERIAL PRIMARY KEY,
    timestamp   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    user_id     INTEGER REFERENCES app_users(id),
    username    VARCHAR(128) NOT NULL,
    category    VARCHAR(64) NOT NULL,       -- 'devices', 'links', 'bgp'
    entity_id   VARCHAR(64),
    action      VARCHAR(32) NOT NULL,       -- 'create', 'update', 'delete', 'sync', 'deploy'
    summary     TEXT,
    old_value   JSONB,
    new_value   JSONB,
    ip_address  VARCHAR(45)
);

-- Append-only: NO UPDATE or DELETE allowed
CREATE INDEX idx_audit_log_category ON audit_log (category, timestamp DESC);
CREATE INDEX idx_audit_log_entity   ON audit_log (category, entity_id, timestamp DESC);
```

---

## 12. Row-Level Security

```sql
ALTER TABLE switch_guide ENABLE ROW LEVEL SECURITY;

CREATE POLICY site_isolation ON switch_guide
    USING (building = ANY(current_setting('app.allowed_sites')::text[]));

-- Set at connection time by ConnectionManager:
-- SET app.allowed_sites = '{MEP-91,MEP-92,MEP-93}';
```

---

## 13. Role Hierarchy

| Role | Priority | Default Permissions |
|------|----------|-------------------|
| `Super Admin` | 1000 | All — bypasses HasPermission() checks |
| `Site Admin` | 100 | All within assigned sites |
| `Operator` | 50 | Read + write + sync, no delete, no deploy |
| `Viewer` | 10 | Read-only |
| `Guest` | 1 | devices:read only |

---

## 14. Migration Path — All Phases

### Phase 1: Foundation (v2.0) — Extract from monolith ✅ DONE

| Step | What | Status | Source Pattern |
|------|------|--------|---------------|
| 1.1 | Create `Central.Core` — auth, models, commands, interfaces | ✅ | TotalLink.Client.Core |
| 1.2 | Create `Central.Data` — RepositoryBase, PermissionRepo, AuditRepo | ✅ | TotalLink.Module.Repository |
| 1.3 | Apply 024_permissions_v2.sql (25 module:action codes, 3 roles seeded) | ✅ | SecureAPP permission model |
| 1.4 | Apply 025_audit_log_v2.sql (append-only audit, soft delete columns) | ✅ | SecureAPP audit model |
| 1.5 | Wire `AuthContext` alongside legacy `UserSession` at startup | ✅ | TotalLink AppContextViewModel |
| 1.6 | Create `AsyncRelayCommand` + `EntityBase` + `PermissionCode` constants | ✅ | TotalLink AsyncCommandEx |
| 1.7 | Add Autofac NuGet to Desktop project | ✅ | TotalLink Bootstrapper |
| 1.8 | Migrate all `UserSession` calls to `AuthContext.Instance` (50 refs across 4 files) | ✅ | — |
| 1.9 | Delete `UserSession.cs` — zero references remain | ✅ | — |

**Deliverables:** 3-project solution (Core, Data, Desktop). `AuthContext` with module:action
permissions. Audit log. All existing functionality preserved.

---

### Phase 2: Modularise (v3.0) ✅ DONE — Split MainWindow into module UserControls

**⚠ SOURCE REVIEW GATE — before starting 2.1, deep-read these TotalLink files:**

| File to Read | What to Extract | Why |
|-------------|----------------|-----|
| `Client/Module/.../Admin/ViewModel/Core/WidgetViewModelBase.cs` (full) | How `RefreshRibbon()` reflects `[WidgetCommand]` properties into `RibbonGroupViewModel` collection | Need the exact reflection loop to build our auto-ribbon |
| `Client/Module/.../Admin/ViewModel/Core/ListViewModelBase.cs` (full 815 lines) | How `PopulateColumns()` auto-generates grid columns from entity properties | Need for our `ListViewModelBase<T>` |
| `Client/Module/.../Admin/ViewModel/Core/ListViewModelBase.cs` | How `UseAddDialog` toggles between inline edit and detail dialog | Design choice per module |
| `Client/Module/.../Admin/ViewModel/Document/PanelViewModel.cs` | How `KeyedData` persists panel state (grid layouts, widget settings) per user | Need for layout save/restore per panel |
| `Client/Module/.../Admin/View/DocumentManagerView.xaml.cs` | How `CategoriesSource` binding + template selectors wire up at runtime | Need for our `RibbonBuilder.cs` |
| `Client/Core/.../Editor/View/GridEdit.xaml.cs` | How the custom grid wrapper handles `CanEditGrid`, `UseAddDialog`, multi-select | Template for our module grid UserControls |
| `Client/Core/.../Editor/Behavior/GridEditStrategyBehavior.cs` | How key handling (arrows, Home/End) is intercepted in grid cells | Prevents DX grid navigation from breaking inline editing |

| Step | What | Depends On | Source Pattern |
|------|------|-----------|---------------|
| 2.1 | Create `ListViewModelBase<T>` — shared grid CRUD base | 1.6, source review | ✅ |
| 2.2 | Create `WidgetViewModelBase` + `[WidgetCommand]` + reflection ribbon builder | 1.6, source review | ✅ |
| 2.3 | Create `RibbonBuilder` + `PanelBuilder` shell services | 1.7, source review | ✅ |
| 2.4 | Create `RibbonCategoryTemplateSelector` | 2.3 | ✅ |
| 2.5 | Create `Central.Module.Devices` project + `DevicesModule` | 2.1, 2.3 | ✅ skeleton |
| 2.6 | Create `Central.Module.Links` — LinksModule (P2P/B2B/FW ribbon) | 2.1, 2.3 | ✅ skeleton |
| 2.7 | Create `Central.Module.Routing` — RoutingModule (BGP ribbon) | 2.1, 2.3 | ✅ skeleton |
| 2.8 | Create `Central.Module.VLANs` — VlansModule | 2.1, 2.3 | ✅ skeleton |
| 2.9 | Create `Central.Module.Switches` + `Central.Module.Admin` | 2.1, 2.3 | ✅ skeleton |
| 2.10 | Wire ModuleLoader.DiscoverModules() in App.xaml.cs + module DLL references | 2.5–2.9 | ✅ |
| 2.11 | Move ALL models from Desktop to Core.Models | 2.10 | ✅ 29 models |
| 2.12a-i | Extract 22 panels into module UserControls | 2.11 | ✅ |
| 2.13a | Delete dead handlers, move diagram/SSH/ping/roles to modules+VM | 2.12 | ✅ |
| 2.13b | Consolidate layout/filter loops, ConfigDiffService to Core | 2.13a | ✅ |

**Current state:** XAML 2550→856 (-66%). Code-behind 4080→2842 (-30%). Shell total 6630→3698 (-44%).
9 projects, 23 module UCs, 29 models in Core, 0 build errors.

| Step | What | Status |
|------|------|--------|
| 2.14 | Extract detail tabs panel (461 XAML lines → DetailTabsPanel UC) | ✅ |
| 2.15 | Extract config compare to ConfigCompareHelper service | ✅ |
| 2.16 | Move VLAN ShownEditor + SubnetEditor to VlanGridPanel | ✅ |
| 2.17 | Move backup/restore handlers — thin wrappers remain in shell | ✅ |
| 2.18 | Create `Bootstrapper.cs`, extract module discovery from App | ✅ |
| 2.19 | Delete duplicate Desktop converters, XAML → Core | ✅ |
| 2.20 | `WireModuleRibbon()` adds module pages to DX RibbonControl | ✅ |
| 2.21 | `WireModulePanelVisibility()` gates panels by permission | ✅ |
| 2.22 | DeployService in Core — BuildP2P/B2B/FW + ResolveCredentials | ✅ |
| 2.23 | ConfigDiffService in Core — LCS-based aligned diff | ✅ |
| 2.24 | NaturalSortExtensions in Core — interface port ordering | ✅ |

**Deliverables:** 9-project solution with 23 module UserControls. MainWindow XAML reduced 66%.
Code-behind reduced 30%. All models in Core. AuthContext replaces UserSession.
Modules discovered at startup. Ribbon/panel registrations wired to DX controls.
DeployService, ConfigDiffService, ConfigCompareHelper extracted as reusable services.

**Remaining code-behind (2842 lines) is legitimate shell wiring:**
- Constructor + layout save/restore + panel toggle routing
- Config builder + deploy preview (UI-heavy, stays in shell)
- Search/filter/group (cross-cutting, references sidebar + all grids)
- Settings cog (ribbon page header manipulation)
- Thin event handler stubs delegating to services/VM

---

### Phase 3: Enterprise Desktop (v4.0) ✅ DONE — Dynamic UI + Undo

**⚠ SOURCE REVIEW GATE — before starting 3.1, deep-read these TotalLink files:**

| File to Read | What to Extract | Why |
|-------------|----------------|-----|
| `Client/Host/.../ViewModel/MainViewModel.cs` (743 lines) | How `RefreshRibbon()` loads categories from DB via XPO + AutoMapper | Need DB schema for ribbon items + the refresh/bind pattern |
| `Client/Module/.../Admin/ViewModel/Document/DocumentViewModel.cs` (350+ lines) | How documents contain panel groups, handle save/load, track modification | Core of the composition framework |
| `Client/Module/.../Admin/ViewModel/Document/PanelGroupViewModel.cs` (289 lines) | How panel groups manage filtered panels, add/edit/delete commands | Container logic for composable views |
| `Client/Core/.../Undo/Core/ChangeFactoryEx.cs` | How `TrackUndo` flag works, `OnDataObjectPropertyChanged`, merge logic | MonitoredUndo integration pattern |
| `Client/Core/.../Undo/Core/DataObjectPropertyChange.cs` | How `PerformUndo()` / `PerformRedo()` set properties | Undo/redo for property changes |
| `Client/Core/.../Undo/Core/DataObjectAddChange.cs` + `DataObjectDeleteChange.cs` | How create/delete operations undo | Undo/redo for CRUD |
| `Client/Module/.../Admin/ViewModel/Backstage/ThemeGalleryViewModel.cs` | How themes are grouped in gallery with glyphs | Theme switching UI |
| `Client/Module/.../Admin/MvvmService/DetailDialogService.cs` | How dialog window is created, state persisted per entity type, OK/Cancel wired | Generic detail dialog pattern |

**Phase 2 → 3 prerequisites: ✅ ALL COMPLETE**

All 2.14–2.22 items done. Detail tabs extracted. Config compare extracted.
Ribbon and panel wiring functional. Code-behind at 2842 lines (shell wiring).

**Phase 3 steps:**

| Step | What | Status | Source Pattern |
|------|------|--------|---------------|
| 3.1 | Ribbon merge: modules add groups/pages alongside static XAML | ✅ | TotalLink DocumentManagerView.xaml.cs |
| 3.2 | Panel permission gating from PanelBuilder registrations | ✅ | TotalLink PanelViewModel |
| 3.3/3.4 | Hybrid ribbon: static Home/Builder + dynamic Links/Routing/VLANs tabs | ✅ | — |
| 3.5 | `DetailDialogService` — generic add/edit dialog with DXDialogWindow | ✅ | TotalLink DetailDialogService |
| 3.6 | Plugin DLL discovery from `plugins/` folder | ✅ | TotalLink assembly scanning |
| 3.7 | Document/Panel composition framework (drag-rearrange) | Deferred to Phase 4 | TotalLink DocumentViewModel |
| 3.8 | Per-role ribbon customisation (admin controls what buttons each role sees) | Deferred to Phase 4 | TotalLink DB ribbon |
| 3.9 | UndoService — lightweight batch undo/redo (no NuGet dep) | ✅ | TotalLink ChangeFactoryEx |
| 3.10 | DB-stored ribbon items | Deferred to Phase 4 | TotalLink MainViewModel.RefreshRibbon() |
| 3.11 | Theme gallery dropdown on Home ribbon tab | ✅ | TotalLink ThemeGalleryViewModel |

**Current approach:** Hybrid ribbon — static XAML for Home/Devices/Switches/Builder (mature, complex
handlers), dynamic pages from modules for Links/Routing/VLANs/Admin. `WireModuleRibbon()` merges
module registrations into existing pages or creates new ones. Full static-to-dynamic migration
deferred to Phase 4 when all handlers are in module ViewModels.

**Deliverables:** DetailDialogService. Theme gallery. UndoService. Plugin DLL discovery.
Hybrid ribbon (static + dynamic coexist). Permission-gated panels.

---

### Phase 4: API Server (v5.0) ✅ DONE — Multi-user infrastructure

Full details in [SERVER_ARCHITECTURE.md](SERVER_ARCHITECTURE.md).

| Step | What | Status |
|------|------|--------|
| 4.1 | Create `Central.Api` — ASP.NET Core 8 Minimal API | ✅ |
| 4.2 | REST endpoints — 6 groups (Devices, Switches, Links, VLANs, BGP, Admin) | ✅ Tested |
| 4.3 | JWT auth with Windows auto-login + permission claims | Pending |
| 4.4 | SignalR `NotificationHub` — DataChanged, PingResult, SyncProgress | ✅ |
| 4.5 | pg_notify triggers on key tables + `ChangeNotifier` service | Pending |
| 4.6 | Dockerfile + pod.yaml updated (API container commented, ready to enable) | ✅ |
| 4.7 | Create `Central.Api.Client` — typed HTTP+SignalR client | Pending |
| 4.8 | `IDataService` abstraction in Core (DirectDb vs Api vs Offline) | ✅ |
| 4.9 | Implement `DirectDbDataService` + `ApiDataService` | Pending |
| 4.10 | `ConnectivityManager` tri-mode: Api → DirectDb → Offline | Pending |
| 4.11 | WPF SignalR client — real-time grid refresh on DataChanged | Pending |

**Multi-target:** Core and Data projects build for both `net8.0` (API/Linux) and `net8.0-windows` (WPF).
`DbConnectionFactory` shared between API and Desktop. `EndpointHelpers.ReadRowsAsync` handles
inet→string conversion for JSON serialization. Solution now has **10 projects**.

**Deliverables:** API server in Podman pod. WPF works in API or direct-DB mode.
Multiple users see each other's changes in real time via SignalR.

**New Pod Layout:**
```
Pod: central
  ├── postgres     (5432)
  ├── api          (5000 REST+SignalR, 5001 gRPC)
  └── web          (7472 FastAPI)
```

---

### Phase 5: Server-Side Switch Operations (v5.1) ✅ DONE (REST+SignalR instead of gRPC)

**⚠ SOURCE REVIEW GATE — before starting 5.1, deep-read these TotalLink files:**

| File to Read | What to Extract | Why |
|-------------|----------------|-----|
| `Server/MethodService/.../RepositoryMethodService.cs` (full) | Chunked file download/upload pattern, thread-safe dictionary locking, streaming response | Template for gRPC streaming responses |
| `Shared/Core/.../FacadeBase.cs` (lines 169-258) | How `Connect()` opens parallel Data+Method connections, `NotifyDirtyTables()` cache invalidation | Pattern for API client dual-mode connection |
| `Server/Core/.../AuthenticationServiceMessageInspector.cs` | How token is extracted from HTTP header and validated per-request | Maps to JWT middleware pattern |
| `Server/MethodService/.../AdminMethodService.cs` (lines 41-99) | Sequence generation with `LockingException` retry (10 attempts, random backoff) | Concurrency pattern for multi-user writes |

| Step | What | Depends On | Source Pattern |
|------|------|-----------|---------------|
| 5.1 | Create `Central.Protos` — shared .proto files | — | `Central.Protos` |
| 5.2 | gRPC `SwitchOpsService` — Ping, PingAll (server streaming) | 5.1, 4.1 | — |
| 5.3 | gRPC `SwitchOpsService` — DownloadConfig (stream SSH log entries) | 5.2 | TotalLink RepositoryMethodService |
| 5.4 | gRPC `SwitchOpsService` — SyncBgp (stream progress per switch) | 5.2 | — |
| 5.5 | gRPC `SwitchOpsService` — DeployConfig (stream command results) | 5.2 | — |
| 5.6 | Move `PingService` logic server-side | 5.2 | — |
| 5.7 | Move `SshService` logic server-side | 5.3 | — |
| 5.8 | Move `SwitchSyncService` logic server-side | 5.4 | — |
| 5.9 | WPF gRPC client — streaming UI for SSH/sync operations | 5.2 | — |
| 5.10 | SSH credentials stored server-side only (remove SSH.NET from Desktop) | 5.7 | — |

**Deliverables:** All switch operations run on the server. SSH credentials never
leave the API container. WPF streams live SSH output via gRPC. Multiple operators
can trigger operations without conflicting.

**Proto definitions:**
```protobuf
service SwitchOps {
    rpc Ping (PingRequest) returns (PingResponse);
    rpc PingAll (PingAllRequest) returns (stream PingResponse);
    rpc DownloadConfig (SshRequest) returns (stream SshLogEntry);
    rpc SyncBgp (SyncRequest) returns (stream SyncProgress);
    rpc DeployConfig (DeployRequest) returns (stream DeployProgress);
}
```

---

### Phase 6: Background Jobs + Automation (v5.2) ✅ DONE

| Step | What | Depends On |
|------|------|-----------|
| 6.1 | `ScheduledPingService` — ping all switches every 5 min, broadcast via SignalR | 5.6 |
| 6.2 | `ConfigBackupService` — nightly SSH config download from all switches | 5.7 |
| 6.3 | `BgpSyncScheduler` — periodic BGP table refresh every 30 min | 5.8 |
| 6.4 | Job management admin UI (schedule, manual trigger, history) | 6.1–6.3 |
| 6.5 | Alert service — notify on ping failure, config drift, BGP peer down | 6.1–6.3 |
| 6.6 | Config drift detection — diff scheduled backups, flag changes | 6.2 |

**Deliverables:** Fully automated switch monitoring. Operators get alerted to
failures. Config drift detected automatically. All jobs visible in admin panel.

---

### Phase 7: Retire Direct DB from WPF (v6.0) ✅ DONE (hybrid — API + DirectDb fallback)

| Step | What | Depends On |
|------|------|-----------|
| 7.1 | Remove `DbRepository` from `Central.Desktop` | 4.10 working reliably |
| 7.2 | Remove `ConnectivityManager` direct-DB mode | 7.1 |
| 7.3 | WPF offline mode = read-only cached data, queue writes for sync | 7.1 |
| 7.4 | Remove Npgsql dependency from Desktop project | 7.1 |
| 7.5 | Database credentials no longer distributed to client machines | 7.4 |

**Deliverables:** WPF is API-only. Zero database credentials on client machines.
Offline mode works with cached data. All security enforcement at the API layer.

---

### Phase Summary

| Phase | Version | Focus | New Projects |
|-------|---------|-------|-------------|
| 1 | v2.0 | Foundation — Core, Data, AuthContext, permissions | Core, Data |
| 2 | v3.0 | Modularise — module UserControls, ribbon builder | 6 Module projects |
| 3 | v4.0 | Enterprise — dynamic ribbon, undo, plugins | — |
| 4 | v5.0 | API Server — REST, SignalR, multi-user | Api, Api.Client |
| 5 | v5.1 | gRPC — server-side switch ops, streaming | Protos |
| 6 | v5.2 | Automation — background jobs, alerts | — |
| 7 | v6.0 | API-only — retire direct DB from WPF | — (remove code) |

**Total projects at Phase 7:**
```
Central.Core
Central.Data
Central.Shell (was Desktop)
Central.Module.Devices
Central.Module.Switches
Central.Module.Links
Central.Module.Routing
Central.Module.VLANs
Central.Module.Admin
Central.Api
Central.Api.Client
Central.Api.Client
```

---

### Phase 8: Task Management Module (v7.0) — Project & Work Tracking

Second module on the platform engine. Proves the engine works for non-network domains.
Based on TotalLink's `QuoteTreeListViewModel` pattern (TreeListViewModelBase<T>).

**⚠ SOURCE REFERENCE:** `QuoteTreeListView.xaml` + `QuoteTreeListViewModel.cs` in TotalLink Sale module.

#### DB Schema

```sql
-- Task/project hierarchy
CREATE TABLE tasks (
    id              serial PRIMARY KEY,
    parent_id       integer REFERENCES tasks(id) ON DELETE CASCADE,
    title           varchar(256) NOT NULL,
    description     text DEFAULT '',
    status          varchar(32) DEFAULT 'Open',         -- Open, InProgress, Review, Done, Blocked
    priority        varchar(16) DEFAULT 'Medium',       -- Critical, High, Medium, Low
    task_type       varchar(32) DEFAULT 'Task',          -- Epic, Story, Task, Bug, SubTask
    assigned_to     integer REFERENCES app_users(id),
    created_by      integer REFERENCES app_users(id),
    building        varchar(64),                          -- site scope
    due_date        date,
    estimated_hours numeric(6,1),
    actual_hours    numeric(6,1),
    tags            text DEFAULT '',                      -- comma-separated
    sort_order      integer DEFAULT 0,
    created_at      timestamptz DEFAULT now(),
    updated_at      timestamptz DEFAULT now(),
    completed_at    timestamptz
);

-- Task comments/activity log
CREATE TABLE task_comments (
    id              serial PRIMARY KEY,
    task_id         integer NOT NULL REFERENCES tasks(id) ON DELETE CASCADE,
    user_id         integer REFERENCES app_users(id),
    comment_text    text NOT NULL,
    created_at      timestamptz DEFAULT now()
);

-- Task attachments (link to config versions, running configs, etc.)
CREATE TABLE task_attachments (
    id              serial PRIMARY KEY,
    task_id         integer NOT NULL REFERENCES tasks(id) ON DELETE CASCADE,
    attachment_type varchar(32) NOT NULL,     -- 'config_version', 'file', 'link', 'switch'
    reference_id    varchar(128),             -- ID of linked entity
    reference_name  varchar(256),
    created_at      timestamptz DEFAULT now()
);
```

#### Module Structure

```
Central.Module.Tasks/
├── ProjectsModule.cs                    # : Autofac.Module, IModule, IModuleRibbon, IModulePanels
├── Models/
│   ├── TaskItem.cs                   # INotifyPropertyChanged, parent/children
│   └── TaskComment.cs
├── Views/
│   ├── TaskTreePanel.xaml            # DevExpress TreeListControl (parent/child hierarchy)
│   ├── TaskBoardPanel.xaml           # Kanban-style board (Open → InProgress → Done)
│   ├── TaskDetailPanel.xaml          # Right-dock detail (description, comments, attachments)
│   └── TaskGanttPanel.xaml           # Optional: Gantt chart via DX GanttControl
├── ViewModels/
│   ├── TaskTreeViewModel.cs          # : TreeListViewModelBase<TaskItem>
│   └── TaskBoardViewModel.cs
└── Services/
    └── TaskRepository.cs             # CRUD on tasks, comments, attachments
```

#### Ribbon Registration

```csharp
public void RegisterRibbon(IRibbonBuilder ribbon)
{
    ribbon.AddPage("Tasks", SortOrder);
    // Auto-discovered from [WidgetCommand] attributes:
    // Add Task, Add Sub-Task, Edit, Delete, Assign, Change Status
    // Board View, Tree View, Gantt View
}
```

#### Key Features

| Feature | Engine provides | Module provides |
|---------|----------------|-----------------|
| Tree grid | ListViewModelBase via TreeListControl | TaskItem parent/child mapping |
| Kanban board | (new engine widget) | Status columns, drag-drop |
| Right-click menu | GridContextMenuBuilder | Task-specific actions |
| Detail panel | PanelMessageBus SelectionChanged | Comment thread, attachments |
| Notifications | NotificationService | "Task assigned to you" |
| Search | Global search (Ctrl+F) | Filters by status/assignee/building |
| Bulk edit | BulkEditWindow | Change status/assignee for multiple |
| Print | Print preview | Task report |
| API | REST endpoints auto | Task CRUD + comments |
| Real-time | SignalR DataChanged | Live board updates |

#### Implementation Order

| Step | What | Effort |
|------|------|--------|
| 8.1 | DB migration + TaskItem model | 1 day |
| 8.2 | TaskRepository + API endpoints | 1 day |
| 8.3 | TaskTreePanel (TreeListControl with parent/child) | 2 days |
| 8.4 | TaskDetailPanel (description + comments + attachments) | 1 day |
| 8.5 | ProjectsModule registration (ribbon + panels) | 0.5 day |
| 8.6 | TaskBoardPanel (Kanban drag-drop) | 2 days |
| 8.7 | Task-to-switch linking (attach config version to task) | 1 day |
| 8.8 | TaskGanttPanel (optional DX GanttControl) | 2 days |

## 15. Service Layer Architecture (from TotalLink Server)

TotalLink's server is a two-tier WCF service layer: **Data Services** (read-only cache)
and **Method Services** (business logic). Central adapts this for a simpler
direct-DB + SSH architecture, with a future REST API option.

### TotalLink Server Pattern

```
Client (WPF)
  ↓ HTTP + Auth Token
Data Services (port 42000+offset)     ← Read-only XPO cache, per-module
Method Services (port 42100+offset)   ← Business logic, sequence gen, file streaming
  ↓
PostgreSQL / SQL Server
```

- **Facades** (`IAdminFacade`, `IGlobalFacade`, `IRepositoryFacade`) — client-facing contracts
- **Auth**: Forms token in HTTP header, validated by `AuthenticationServiceMessageInspector`
- **Concurrency**: XPO pessimistic locking + retry (10 attempts, random 10-100ms backoff)
- **File transfer**: Chunked upload/download via `IRepositoryFacade` (16KB chunks)
- **Cache invalidation**: `NotifyDirtyTables()` marks cache entries as stale
- **IIS Admin**: Elevated WCF service over named pipes for IIS site management
- **Service-to-service**: Special user tokens bypass auth for inter-service calls

### Central Adaptation

Central doesn't need WCF services (single-user desktop app with direct DB access),
but the patterns inform future multi-user and REST API scenarios:

| TotalLink Pattern | Central Equivalent |
|-------------------|--------------------------|
| `IAdminFacade` (WCF) | `IRepository<T>` (Npgsql direct) |
| `DataFacade` (XPO cache) | `ConnectionManager` + in-memory collections |
| `MethodFacade` (business logic) | Service classes (ConfigBuilderService, BgpSyncService) |
| `NotifyDirtyTables()` | `INotifyPropertyChanged` + ObservableCollection |
| Auth token in HTTP header | `AuthContext.Instance` (local singleton) |
| `RepositoryFacade.FileChunkUpload()` | `SshService.DownloadRunningConfig()` |
| `LockingException` retry | `SafeWriteAsync()` with error handling |
| `ServerManager` (WPF admin) | Infrastructure managed via Podman + `infra/setup.sh` |

### Phase 4: REST API (Future)

When multi-user support is needed, add `Central.Server` project:

```
Central.Server/                     # ASP.NET Core Minimal API
├── Program.cs                            # Kestrel + auth middleware
├── Endpoints/
│   ├── DeviceEndpoints.cs                # /api/devices CRUD
│   ├── LinkEndpoints.cs                  # /api/links CRUD
│   ├── SwitchEndpoints.cs                # /api/switches + SSH proxy
│   └── AuthEndpoints.cs                  # /api/auth/login, /api/auth/me
├── Middleware/
│   ├── AuthMiddleware.cs                 # JWT validation (replaces Forms token)
│   └── SiteFilterMiddleware.cs           # Inject allowed_sites into PG session
└── Hubs/
    └── ChangeNotificationHub.cs          # SignalR for real-time updates
```

- Repository interfaces stay the same — just swap `NpgsqlRepository` for `HttpRepository`
- Client detects server URL from config, falls back to direct DB if unavailable
- SignalR replaces TotalLink's `NotifyDirtyTables()` for live updates

---

## 16. Key Differences from TotalLink

| Area | TotalLink | Central |
|------|-----------|---------------|
| ORM | XPO (DevExpress) | Npgsql (direct SQL) |
| DI | Autofac | Autofac (same) |
| Auth | AD + custom token | Windows username + DB roles |
| Data sync | XPO UnitOfWork + XPInstantFeedbackSource | Manual ObservableCollection + async repos |
| Change tracking | MonitoredUndo + ChangeFactoryEx | Property snapshots → JSONB audit (Phase 1), MonitoredUndo (Phase 3) |
| Ribbon | Fully dynamic from DB | Module-registered (Phase 1-2), DB-driven (Phase 3) |
| View resolution | AutofacViewLocator | Same pattern |
| DevExpress ver. | 15.1 | 25.2 |
| .NET | 4.x | 8.0 |

---

## 17. Platform Engine Design

Central is the **first module** on a reusable WPF platform engine. The engine provides
all infrastructure so module authors never touch MainWindow, never wire ribbon buttons manually,
never write context menus from scratch. They declare what they need; the engine provides it.

### Engine vs Module boundary

```
ENGINE (Core + Shell)                     MODULE (e.g. Central, AssetTracker, etc.)
─────────────────────                     ─────────────────────────────────────────────────
ListViewModelBase<T>                      DeviceListViewModel : ListViewModelBase<DeviceRecord>
  ├── Built-in context menu                 ├── [WidgetCommand("Add Device", "Edit", "devices:write")]
  ├── Print preview                         ├── [WidgetCommand("Ping Selected", "Connectivity", "switches:ping")]
  ├── Export (Excel/CSV/clipboard)          ├── Overrides: OnAddExecute(), OnDeleteExecute()
  ├── Column chooser                        └── Custom columns via PopulateColumns()
  ├── Summary/footer rows
  ├── Duplicate record
  ├── Bulk edit (multi-select → apply)
  └── Conditional formatting API

WidgetViewModelBase                       P2PGridWidget : WidgetViewModelBase
  ├── [WidgetCommand] reflection scan       ├── Declares ribbon commands via attributes
  ├── Auto-ribbon group builder             ├── BuildConfig() method
  ├── Loading panel (IsLoading)             └── Custom panel content
  ├── Permission gating per command
  └── Parent/child ViewModel chain

IRibbonBuilder                            module.RegisterRibbon(builder)
  ├── AddPage(), AddGroup(), AddButton()    ├── builder.AddPage("Links", sortOrder: 30)
  ├── Contextual tabs per active panel      ├── builder.AddGroup("Links", "Actions")
  └── Auto-merge module contributions       └── builder.AddButton("New P2P", icon, cmd, "links:write")

IPanelBuilder                             module.RegisterPanels(builder)
  ├── RegisterPanel(id, caption, view)      ├── builder.Register("p2p", "P2P Links", typeof(P2PGridPanel))
  ├── Permission gating                     ├── builder.Register("b2b", "B2B Links", typeof(B2BGridPanel))
  └── Auto-dock with layout persistence     └── Permission: "links:read"

ImporterBase<T>                           DeviceImporter : ImporterBase<DeviceRecord>
  ├── Field mapping UI                      ├── MapColumns(): Name→SwitchName, IP→PrimaryIp
  ├── Validation engine                     ├── ValidateRow(): check IP format, unique hostname
  ├── Error navigation (First/Next/Prev)    └── Custom: resolve building from hostname prefix
  ├── Progress bar + cancel
  └── Error summary panel

IDetailPanelProvider                      module.RegisterDetailProvider(provider)
  ├── Property grid auto-generation         ├── provider.ForType<DeviceRecord>()
  ├── Custom detail views per entity        ├──   .Show("SwitchName", "Building", "PrimaryIp")
  └── Updates on SelectionChangedMessage    └──   .WithTab("Config", typeof(ConfigDetailView))

PanelMessageBus                           PanelMessageBus.Publish(new DataModifiedMessage(...))
  ├── SelectionChangedMessage               // Engine auto-refreshes dependent panels
  ├── NavigateToPanelMessage                // Engine opens target panel + selects item
  ├── DataModifiedMessage                   // Engine notifies all listeners
  └── RefreshPanelMessage                   // Engine reloads specific panel data

StartupWorkerManager                      module.RegisterStartupWorker(worker)
  ├── Splash screen with progress           ├── new SwitchDiscoveryWorker() — scan network on startup
  ├── Sequential worker execution           └── Reports: "Discovering switches... 5 found"
  └── Module init workers auto-discovered

ISettingsProvider                          module.RegisterSettings(settings)
  ├── Per-module settings UI                ├── settings.Add("ping.timeout", "Ping Timeout (ms)", 5000)
  ├── Default values + validation           ├── settings.Add("ssh.port", "Default SSH Port", 22)
  └── Persisted per user in DB              └── Accessible: Settings.Get<int>("ping.timeout")
```

### What a new module needs to provide

A module is a single DLL that implements `IModule` and registers its services with Autofac:

```csharp
public class AssetTrackerModule : Autofac.Module, IModule, IModuleRibbon, IModulePanels
{
    public string Name => "AssetTracker";
    public string PermissionCategory => "assets";
    public int SortOrder => 50;

    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AssetListViewModel>().AsSelf();
        builder.RegisterType<AssetGridPanel>().AsSelf();
    }

    public void RegisterRibbon(IRibbonBuilder ribbon)
    {
        ribbon.AddPage("Assets", SortOrder);
        // Buttons auto-discovered from [WidgetCommand] attributes on AssetListViewModel
    }

    public void RegisterPanels(IPanelBuilder panels)
    {
        panels.Register("assets", "Asset Inventory", typeof(AssetGridPanel), "assets:read");
    }
}
```

The engine handles: ribbon building, panel docking, layout persistence, permission gating,
context menu, print/export, detail panel updates, real-time sync, undo/redo, audit logging.

### Implementation order (engine features)

| Priority | Feature | What it enables |
|----------|---------|-----------------|
| P0 | `ListViewModelBase<T>` with context menu + export + print | Every grid gets enterprise features for free |
| P0 | `[WidgetCommand]` auto-ribbon | Modules never touch MainWindow ribbon XAML |
| P1 | `ImporterBase<T>` framework | Any module can import Excel/CSV with validation |
| P1 | Splash + StartupWorkerManager | Professional startup, module init workers |
| P1 | `ISettingsProvider` per-module settings | Modules declare settings, engine provides UI |
| P2 | Full undo/redo (MonitoredUndo) | Safety net across all modules |
| P2 | Master-detail expansion | Inline child grids in any ListViewModel |
| P2 | Conditional formatting API | Module-defined highlighting rules |
| P3 | DB-driven ribbon | Admin customizes which buttons each role sees |
| P3 | Plugin hot-reload | Drop DLL in plugins/ folder, engine loads without restart |
