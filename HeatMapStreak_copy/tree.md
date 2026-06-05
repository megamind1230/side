.
├── Components
│   └── Shared
├── HeatMapStreak.slnx
├── HeatMapStreak.Web
│   ├── appsettings.Development.json
│   ├── appsettings.json
│   ├── bin
│   │   └── Debug
│   │       └── net8.0
│   ├── Components
│   │   ├── Account
│   │   │   ├── IdentityComponentsEndpointRouteBuilderExtensions.cs
│   │   │   ├── IdentityNoOpEmailSender.cs
│   │   │   ├── IdentityRedirectManager.cs
│   │   │   ├── IdentityRevalidatingAuthenticationStateProvider.cs
│   │   │   ├── IdentityUserAccessor.cs
│   │   │   ├── Pages
│   │   │   └── Shared
│   │   ├── App.razor
│   │   ├── _Imports.razor
│   │   ├── Layout
│   │   │   ├── MainLayout.razor
│   │   │   ├── MainLayout.razor.css
│   │   │   ├── NavMenu.razor
│   │   │   └── NavMenu.razor.css
│   │   ├── Pages
│   │   │   ├── Dashboard.razor
│   │   │   ├── Error.razor
│   │   │   ├── Export.razor
│   │   │   ├── Habits.razor
│   │   │   └── Home.razor
│   │   ├── Routes.razor
│   │   └── Shared
│   │       ├── AggregatedHeatmap.razor
│   │       ├── DarkModeToggle.razor
│   │       ├── Heatmap.razor
│   │       └── StreakBadge.razor
│   ├── Data
│   │   ├── app.db
│   │   ├── ApplicationDbContext.cs
│   │   ├── ApplicationUser.cs
│   │   └── Migrations
│   │       ├── 00000000000000_CreateIdentitySchema.cs
│   │       ├── 00000000000000_CreateIdentitySchema.Designer.cs
│   │       ├── 20260515202508_InitialCreate.cs
│   │       ├── 20260515202508_InitialCreate.Designer.cs
│   │       └── ApplicationDbContextModelSnapshot.cs
│   ├── Data\app.db
│   ├── Data\app.db-shm
│   ├── Data\app.db-wal
│   ├── HeatMapStreak.Web.csproj
│   ├── Models
│   │   ├── DayEntry.cs
│   │   ├── GoalPeriod.cs
│   │   ├── GoalType.cs
│   │   └── Habit.cs
│   ├── obj
│   │   ├── Debug
│   │   │   └── net8.0
│   │   ├── HeatMapStreak.Web.csproj.nuget.dgspec.json
│   │   ├── HeatMapStreak.Web.csproj.nuget.g.props
│   │   ├── HeatMapStreak.Web.csproj.nuget.g.targets
│   │   ├── project.assets.json
│   │   └── project.nuget.cache
│   ├── Program.cs
│   ├── Properties
│   │   └── launchSettings.json
│   ├── Services
│   │   ├── HabitService.cs
│   │   ├── IHabitService.cs
│   │   ├── IStreakService.cs
│   │   └── StreakService.cs
│   └── wwwroot
│       ├── app.css
│       ├── bootstrap
│       │   ├── bootstrap.min.css
│       │   └── bootstrap.min.css.map
│       ├── favicon.png
│       └── js
│           └── site.js
├── Models
├── Services
├── tree.md
└── wwwroot
    └── css

29 directories, 57 files
