using CommunityToolkit.Mvvm.Messaging;
using FluentAssertions;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Messages;
using GoogleCalendarManagement.Models;
using GoogleCalendarManagement.Services;
using GoogleCalendarManagement.ViewModels;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.Tests.Unit.ViewModels;

[Collection("Messenger")]
public sealed class DataSourcePanelViewModelTests : IDisposable
{
    public DataSourcePanelViewModelTests()
    {
        WeakReferenceMessenger.Default.Reset();
    }

    [Fact]
    public async Task LoadSources_WhenRepositoryReturnsEmpty_ShowsEmptyState()
    {
        var viewModel = CreateViewModel(new StubDataSourceRepository());

        await viewModel.InitializeAsync();

        viewModel.Sources.Should().BeEmpty();
        viewModel.EmptyGlobalStateVisibility.Should().Be(Visibility.Visible);
        viewModel.SourceListVisibility.Should().Be(Visibility.Collapsed);
        viewModel.IsLoadingGlobal.Should().BeFalse();
    }

    [Fact]
    public async Task LoadSources_WhenRepositoryReturnsSources_BuildsSummaryViewModels()
    {
        var repository = new StubDataSourceRepository
        {
            Sources =
            [
                new DataSource { DataSourceId = 2, SourceKey = "toggl", DisplayName = "Toggl" },
                new DataSource { DataSourceId = 1, SourceKey = "oura", DisplayName = "Oura" }
            ],
            LastImports =
            {
                [1] = new DataSourceImportLog
                {
                    DataSourceId = 1,
                    CoveredEndDate = new DateOnly(2026, 05, 13),
                    ImportedAt = new DateTime(2026, 05, 15, 09, 00, 00, DateTimeKind.Utc),
                    Success = true
                }
            }
        };

        var viewModel = CreateViewModel(
            repository,
            timeProvider: new FixedTimeProvider(new DateTimeOffset(2026, 05, 15, 12, 00, 00, TimeSpan.Zero)));

        await viewModel.InitializeAsync();

        viewModel.Sources.Select(source => source.DisplayName).Should().Equal("Oura", "Toggl");
        viewModel.Sources[0].LastDataDateLabel.Should().Be("May 13, 2026");
        viewModel.Sources[0].LastImportedRelativeLabel.Should().Be("3 hours ago");
        viewModel.Sources[1].LastDataDateLabel.Should().Be("Never imported");
        viewModel.Sources[1].LastImportedRelativeLabel.Should().BeNull();
        viewModel.EmptyGlobalStateVisibility.Should().Be(Visibility.Collapsed);
        viewModel.SourceListVisibility.Should().Be(Visibility.Visible);
    }

    [Fact]
    public async Task LoadSources_WhenHandlerRegistered_ImportCommandIsEnabled()
    {
        var repository = new StubDataSourceRepository
        {
            Sources = [new DataSource { DataSourceId = 1, SourceKey = "toggl", DisplayName = "Toggl" }]
        };
        var registry = new DataSourceImportHandlerRegistry();
        var handler = new RecordingImportHandler("toggl");
        registry.Register(handler);
        var viewModel = CreateViewModel(repository, registry: registry);

        await viewModel.InitializeAsync();
        var source = viewModel.Sources.Single();

        source.HasImportHandler.Should().BeTrue();
        source.ImportCommand.CanExecute(null).Should().BeTrue();

        await source.ImportCommand.ExecuteAsync(null);

        handler.TriggerCount.Should().Be(1);
    }

    [Fact]
    public async Task LoadSources_WhenNoHandlerRegistered_ImportCommandIsDisabled()
    {
        var repository = new StubDataSourceRepository
        {
            Sources = [new DataSource { DataSourceId = 1, SourceKey = "toggl", DisplayName = "Toggl" }]
        };
        var viewModel = CreateViewModel(repository);

        await viewModel.InitializeAsync();
        var source = viewModel.Sources.Single();

        source.HasImportHandler.Should().BeFalse();
        source.ImportCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task OnImportCompletedMessage_ReloadsSourceList()
    {
        var repository = new StubDataSourceRepository
        {
            Sources = [new DataSource { DataSourceId = 1, SourceKey = "toggl", DisplayName = "Toggl" }]
        };
        var viewModel = CreateViewModel(repository);
        await viewModel.InitializeAsync();
        repository.Sources =
        [
            new DataSource { DataSourceId = 1, SourceKey = "toggl", DisplayName = "Toggl" },
            new DataSource { DataSourceId = 2, SourceKey = "oura", DisplayName = "Oura" }
        ];

        WeakReferenceMessenger.Default.Send(new DataSourceImportCompletedMessage(1, "toggl", true));
        await repository.WaitForGetAllSourcesCallsAsync(2);

        viewModel.Sources.Select(source => source.DisplayName).Should().Equal("Oura", "Toggl");
    }

    [Fact]
    public async Task DaySelectedMessage_WithSelectedDay_KeepsSourcesPanelActive()
    {
        var repository = new StubDataSourceRepository
        {
            Sources = [new DataSource { DataSourceId = 1, SourceKey = "toggl", DisplayName = "Toggl" }]
        };
        var daySelectionService = new StubCalendarDaySelectionService { SelectedDay = null };
        var viewModel = CreateViewModel(repository, daySelectionService: daySelectionService);

        await viewModel.InitializeAsync();
        WeakReferenceMessenger.Default.Send(new DaySelectedMessage(new DateOnly(2026, 05, 13)));

        viewModel.ActivePanel.Should().Be(PanelKind.Sources);
        viewModel.CurrentDay.Should().Be(new DateOnly(2026, 05, 13));
        viewModel.SourcesPanelVisibility.Should().Be(Visibility.Visible);
        viewModel.DayDetailPanelVisibility.Should().Be(Visibility.Collapsed);
        viewModel.DayModeSourceListVisibility.Should().Be(Visibility.Collapsed);
        viewModel.DayCards.Should().ContainSingle(card => card.DisplayName == "Toggl");
    }

    [Fact]
    public async Task SelectDayDetailPanelCommand_ShowsLoadedSelectedDay()
    {
        var repository = new StubDataSourceRepository
        {
            Sources = [new DataSource { DataSourceId = 1, SourceKey = "toggl", DisplayName = "Toggl" }]
        };
        var viewModel = CreateViewModel(repository);

        await viewModel.InitializeAsync();
        WeakReferenceMessenger.Default.Send(new DaySelectedMessage(new DateOnly(2026, 05, 13)));
        viewModel.SelectDayDetailPanelCommand.Execute(null);

        viewModel.ActivePanel.Should().Be(PanelKind.DayDetail);
        viewModel.CurrentDay.Should().Be(new DateOnly(2026, 05, 13));
        viewModel.DayLabel.Should().Be("Wednesday, May 13");
        viewModel.DayModeSourceListVisibility.Should().Be(Visibility.Visible);
        viewModel.DayCards.Should().ContainSingle(card => card.DisplayName == "Toggl");
    }

    [Fact]
    public void SelectDayDetailPanelCommand_WhenNoDaySelected_ShowsPlaceholder()
    {
        var viewModel = CreateViewModel(new StubDataSourceRepository());

        viewModel.SelectDayDetailPanelCommand.Execute(null);

        viewModel.ActivePanel.Should().Be(PanelKind.DayDetail);
        viewModel.DayDetailPlaceholderVisibility.Should().Be(Visibility.Visible);
        viewModel.DayModeSourceListVisibility.Should().Be(Visibility.Collapsed);
        viewModel.DayModeDrilldownVisibility.Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public void SelectLinkingPanelCommand_ShowsLinkingPanel()
    {
        var viewModel = CreateViewModel(new StubDataSourceRepository());

        viewModel.SelectLinkingPanelCommand.Execute(null);

        viewModel.ActivePanel.Should().Be(PanelKind.Linking);
        viewModel.LinkingPanelVisibility.Should().Be(Visibility.Visible);
        viewModel.SourcesPanelVisibility.Should().Be(Visibility.Collapsed);
        viewModel.DayDetailPanelVisibility.Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public async Task ActivePanel_WhenChanged_PersistsToSystemState()
    {
        var systemStateRepository = new StubSystemStateRepository();
        var viewModel = CreateViewModel(
            new StubDataSourceRepository(),
            systemStateRepository: systemStateRepository);

        viewModel.SelectDayDetailPanelCommand.Execute(null);

        (await systemStateRepository.GetAsync("DataSourcePanelActivePanel")).Should().Be("DayDetail");
    }

    [Fact]
    public async Task InitializeAsync_WhenActivePanelStored_RestoresPanel()
    {
        var systemStateRepository = new StubSystemStateRepository
        {
            Values = { ["DataSourcePanelActivePanel"] = "Linking" }
        };
        var viewModel = CreateViewModel(
            new StubDataSourceRepository(),
            systemStateRepository: systemStateRepository);

        await viewModel.InitializeAsync();

        viewModel.ActivePanel.Should().Be(PanelKind.Linking);
        viewModel.LinkingPanelVisibility.Should().Be(Visibility.Visible);
        viewModel.SourcesPanelVisibility.Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public async Task MoveDayCard_WithValidIndexes_ReordersDayCards()
    {
        var repository = new StubDataSourceRepository
        {
            Sources =
            [
                new DataSource { DataSourceId = 1, SourceKey = "toggl", DisplayName = "Toggl" },
                new DataSource { DataSourceId = 2, SourceKey = "maps", DisplayName = "Maps" },
                new DataSource { DataSourceId = 3, SourceKey = "spotify", DisplayName = "Spotify" }
            ]
        };
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadDayModeAsync(new DateOnly(2026, 05, 13));

        viewModel.DayCards.Select(card => card.DisplayName).Should().Equal("Maps", "Spotify", "Toggl");

        viewModel.MoveDayCard(2, 0);

        viewModel.DayCards.Select(card => card.DisplayName).Should().Equal("Toggl", "Maps", "Spotify");
    }

    [Fact]
    public async Task OnDayDeselected_DoesNotChangeActivePanel()
    {
        var repository = new StubDataSourceRepository
        {
            Sources = [new DataSource { DataSourceId = 1, SourceKey = "toggl", DisplayName = "Toggl" }]
        };
        var viewModel = CreateViewModel(repository);

        await viewModel.InitializeAsync();
        viewModel.SelectDayDetailPanelCommand.Execute(null);
        WeakReferenceMessenger.Default.Send(new DaySelectedMessage(new DateOnly(2026, 05, 13)));
        WeakReferenceMessenger.Default.Send(new DaySelectedMessage(null));

        viewModel.ActivePanel.Should().Be(PanelKind.DayDetail);
        viewModel.CurrentDay.Should().BeNull();
        viewModel.DayCards.Should().BeEmpty();
        viewModel.DrilldownCard.Should().BeNull();
        viewModel.DayDetailPlaceholderVisibility.Should().Be(Visibility.Visible);
    }

    [Fact]
    public async Task LoadDayMode_WhenNameEventExists_PopulatesDayName()
    {
        var repository = new StubDataSourceRepository();
        var pendingRepository = new StubPendingEventRepository
        {
            DayNameEvent = new PendingEvent
            {
                PendingEventId = "pending_day_name",
                Summary = "Deep Work",
                IsAllDay = true,
                SourceSystem = "day_name",
                StartDatetime = new DateTime(2026, 05, 13, 0, 0, 0, DateTimeKind.Utc)
            }
        };
        var viewModel = CreateViewModel(repository, pendingEventRepository: pendingRepository);

        await viewModel.LoadDayModeAsync(new DateOnly(2026, 05, 13));

        viewModel.DayName.Should().Be("Deep Work");
        viewModel.HasDayName.Should().BeTrue();
        viewModel.DayNameOrHint.Should().Be("Deep Work");
    }

    [Fact]
    public async Task LoadDayMode_WhenNoNameEvent_DayNameIsNull()
    {
        var viewModel = CreateViewModel(new StubDataSourceRepository());

        await viewModel.LoadDayModeAsync(new DateOnly(2026, 05, 13));

        viewModel.DayName.Should().BeNull();
        viewModel.HasDayName.Should().BeFalse();
        viewModel.DayNameOrHint.Should().Be("Tap to name this day");
    }

    [Fact]
    public async Task ToggleIntegration_CallsRepository_AndUpdatesCheckbox()
    {
        var repository = new StubDataSourceRepository
        {
            Sources = [new DataSource { DataSourceId = 7, SourceKey = "toggl", DisplayName = "Toggl" }]
        };
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadDayModeAsync(new DateOnly(2026, 05, 13));
        var card = viewModel.DayCards.Single();
        await card.ToggleIntegrationCommand.ExecuteAsync(null);

        repository.SetIntegrationCalls.Should().ContainSingle();
        repository.SetIntegrationCalls[0].Should().Be((new DateOnly(2026, 05, 13), 7, true));
        card.IsIntegrated.Should().BeTrue();
    }

    [Fact]
    public async Task LoadDayMode_WhenCardProviderHasNoData_GreysIntegrationCheckbox()
    {
        var repository = new StubDataSourceRepository
        {
            Sources = [new DataSource { DataSourceId = 7, SourceKey = "toggl_sleep", DisplayName = "Toggl Sleep" }]
        };
        var cardProviderRegistry = new DataSourceCardProviderRegistry();
        cardProviderRegistry.Register(new StubCardProvider("toggl_sleep", hasData: false));
        var viewModel = CreateViewModel(repository, cardProviderRegistry: cardProviderRegistry);

        await viewModel.LoadDayModeAsync(new DateOnly(2026, 05, 13));

        var card = viewModel.DayCards.Single();
        card.IsGreyedOut.Should().BeTrue();
        card.IsIntegrationEnabled.Should().BeFalse();
        card.ToggleIntegrationCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task LoadDayMode_UsesIntegrationStatusForSelectedDate()
    {
        var repository = new StubDataSourceRepository
        {
            Sources = [new DataSource { DataSourceId = 7, SourceKey = "toggl", DisplayName = "Toggl" }],
            Integrations =
            {
                [(new DateOnly(2026, 05, 13), 7)] = true,
                [(new DateOnly(2026, 05, 14), 7)] = false
            }
        };
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadDayModeAsync(new DateOnly(2026, 05, 13));
        viewModel.DayCards.Single().IsIntegrated.Should().BeTrue();

        await viewModel.LoadDayModeAsync(new DateOnly(2026, 05, 14));
        viewModel.DayCards.Single().IsIntegrated.Should().BeFalse();
    }

    [Fact]
    public async Task LoadSources_GroupsCurrentViewDataSourcesAndOtherSources()
    {
        var repository = new StubDataSourceRepository
        {
            Sources =
            [
                new DataSource { DataSourceId = 7, SourceKey = "toggl_sleep", DisplayName = "Toggl Sleep" },
                new DataSource { DataSourceId = 8, SourceKey = "oura", DisplayName = "Oura" }
            ]
        };
        var cardProviderRegistry = new DataSourceCardProviderRegistry();
        cardProviderRegistry.Register(new StubCardProvider("toggl_sleep", hasData: true)
        {
            RangeData =
            [
                new DataSourceDayData(new DateOnly(2026, 05, 11), false),
                new DataSourceDayData(new DateOnly(2026, 05, 12), true, 2),
                new DataSourceDayData(new DateOnly(2026, 05, 13), false),
                new DataSourceDayData(new DateOnly(2026, 05, 14), false),
                new DataSourceDayData(new DateOnly(2026, 05, 15), false),
                new DataSourceDayData(new DateOnly(2026, 05, 16), false),
                new DataSourceDayData(new DateOnly(2026, 05, 17), false)
            ]
        });
        var viewModel = CreateViewModel(repository, cardProviderRegistry: cardProviderRegistry);

        await viewModel.LoadSourcesAsync();

        viewModel.SourceDataInViewSources.Select(source => source.DisplayName).Should().Equal("Toggl Sleep");
        viewModel.OtherSources.Select(source => source.DisplayName).Should().Equal("Oura");
        viewModel.SourceDataInViewSources.Single().DayDataMarkers.Should().HaveCount(7);
        viewModel.SourceDataInViewSources.Single().DayDataMarkers[1].CountLabel.Should().Be("2");
    }

    [Fact]
    public async Task CalendarViewRangeChangedMessage_WhenGlobalMode_ReloadsWeekMarkersForNewRange()
    {
        var repository = new StubDataSourceRepository
        {
            Sources = [new DataSource { DataSourceId = 7, SourceKey = "toggl_sleep", DisplayName = "Toggl Sleep" }]
        };
        var rangeProvider = new StubCalendarViewRangeProvider
        {
            Range = (new DateOnly(2026, 05, 11), new DateOnly(2026, 05, 17))
        };
        var cardProviderRegistry = new DataSourceCardProviderRegistry();
        cardProviderRegistry.Register(new StubCardProvider("toggl_sleep", hasData: true)
        {
            RangeDataFactory = (from, _) =>
            [
                new DataSourceDayData(from, true, from.Day == 11 ? 1 : 3),
                new DataSourceDayData(from.AddDays(1), false),
                new DataSourceDayData(from.AddDays(2), false),
                new DataSourceDayData(from.AddDays(3), false),
                new DataSourceDayData(from.AddDays(4), false),
                new DataSourceDayData(from.AddDays(5), false),
                new DataSourceDayData(from.AddDays(6), false)
            ]
        });
        var viewModel = CreateViewModel(
            repository,
            cardProviderRegistry: cardProviderRegistry,
            viewRangeProvider: rangeProvider);
        await viewModel.LoadSourcesAsync();

        rangeProvider.Range = (new DateOnly(2026, 05, 18), new DateOnly(2026, 05, 24));
        WeakReferenceMessenger.Default.Send(new CalendarViewRangeChangedMessage(rangeProvider.Range.From, rangeProvider.Range.To));
        await repository.WaitForGetAllSourcesCallsAsync(2);

        viewModel.SourceDataInViewSources.Single().DayDataMarkers[0].Date.Should().Be(new DateOnly(2026, 05, 18));
        viewModel.SourceDataInViewSources.Single().DayDataMarkers[0].CountLabel.Should().Be("3");
    }

    [Fact]
    public async Task LoadDayMode_WhenProviderSupportsAdd_AddCommandInvokesProviderAction()
    {
        var repository = new StubDataSourceRepository
        {
            Sources = [new DataSource { DataSourceId = 7, SourceKey = "toggl_sleep", DisplayName = "Toggl Sleep" }]
        };
        var provider = new StubCardProvider("toggl_sleep", hasData: true) { SupportsAdd = true };
        var cardProviderRegistry = new DataSourceCardProviderRegistry();
        cardProviderRegistry.Register(provider);
        var viewModel = CreateViewModel(repository, cardProviderRegistry: cardProviderRegistry);

        await viewModel.LoadDayModeAsync(new DateOnly(2026, 05, 13));
        await viewModel.DayCards.Single().AddCommand.ExecuteAsync(null);

        provider.AddedDates.Should().Equal(new DateOnly(2026, 05, 13));
    }

    [Fact]
    public async Task ExpandCard_SetsDrilldownCard()
    {
        var repository = new StubDataSourceRepository
        {
            Sources = [new DataSource { DataSourceId = 1, SourceKey = "toggl", DisplayName = "Toggl" }]
        };
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadDayModeAsync(new DateOnly(2026, 05, 13));
        viewModel.SelectDayDetailPanelCommand.Execute(null);
        var card = viewModel.DayCards.Single();
        card.ExpandCommand.Execute(null);

        viewModel.DrilldownCard.Should().BeSameAs(card);
        viewModel.DayModeSourceListVisibility.Should().Be(Visibility.Collapsed);
        viewModel.DayModeDrilldownVisibility.Should().Be(Visibility.Visible);
    }

    [Fact]
    public async Task BackFromDrilldown_ClearsDrilldownCard()
    {
        var repository = new StubDataSourceRepository
        {
            Sources = [new DataSource { DataSourceId = 1, SourceKey = "toggl", DisplayName = "Toggl" }]
        };
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadDayModeAsync(new DateOnly(2026, 05, 13));
        viewModel.SelectDayDetailPanelCommand.Execute(null);
        viewModel.DayCards.Single().ExpandCommand.Execute(null);
        viewModel.BackFromDrilldownCommand.Execute(null);

        viewModel.DrilldownCard.Should().BeNull();
        viewModel.DayModeSourceListVisibility.Should().Be(Visibility.Visible);
        viewModel.DayModeDrilldownVisibility.Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public async Task LoadDayMode_WhenDrilledDown_RestoresDrilldownOnDayChange()
    {
        var repository = new StubDataSourceRepository
        {
            Sources = [new DataSource { DataSourceId = 1, SourceKey = "toggl", DisplayName = "Toggl" }]
        };
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadDayModeAsync(new DateOnly(2026, 05, 13));
        viewModel.SelectDayDetailPanelCommand.Execute(null);
        viewModel.DayCards.Single().ExpandCommand.Execute(null);
        viewModel.DrilldownCard.Should().NotBeNull();

        await viewModel.LoadDayModeAsync(new DateOnly(2026, 05, 14));

        viewModel.DrilldownCard.Should().NotBeNull();
        viewModel.DrilldownCard!.SourceKey.Should().Be("toggl");
        viewModel.DayModeDrilldownVisibility.Should().Be(Visibility.Visible);
        viewModel.DayModeSourceListVisibility.Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public async Task LoadDayMode_WhenNotDrilledDown_DoesNotRestoreDrilldownOnDayChange()
    {
        var repository = new StubDataSourceRepository
        {
            Sources = [new DataSource { DataSourceId = 1, SourceKey = "toggl", DisplayName = "Toggl" }]
        };
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadDayModeAsync(new DateOnly(2026, 05, 13));
        viewModel.SelectDayDetailPanelCommand.Execute(null);
        await viewModel.LoadDayModeAsync(new DateOnly(2026, 05, 14));

        viewModel.DrilldownCard.Should().BeNull();
        viewModel.DayModeSourceListVisibility.Should().Be(Visibility.Visible);
    }

    [Fact]
    public async Task SourceDataInViewHeader_ShowsCount()
    {
        var repository = new StubDataSourceRepository
        {
            Sources =
            [
                new DataSource { DataSourceId = 7, SourceKey = "toggl_sleep", DisplayName = "Toggl Sleep" },
                new DataSource { DataSourceId = 8, SourceKey = "oura", DisplayName = "Oura" }
            ]
        };
        var cardProviderRegistry = new DataSourceCardProviderRegistry();
        cardProviderRegistry.Register(new StubCardProvider("toggl_sleep", hasData: true)
        {
            RangeData = [new DataSourceDayData(new DateOnly(2026, 05, 12), true, 1)]
        });
        var viewModel = CreateViewModel(repository, cardProviderRegistry: cardProviderRegistry);

        await viewModel.LoadSourcesAsync();

        viewModel.SourceDataInViewHeader.Should().Be("Source data in view (1)");
    }

    [Fact]
    public async Task SourceDataInViewSection_AlwaysVisibleWithZeroItems()
    {
        var viewModel = CreateViewModel(new StubDataSourceRepository());

        await viewModel.LoadSourcesAsync();

        viewModel.SourceDataInViewSources.Should().BeEmpty();
        viewModel.SourceDataInViewVisibility.Should().Be(Visibility.Visible);
        viewModel.SourceDataInViewHeader.Should().Be("Source data in view (0)");
    }

    [Fact]
    public async Task ToggleSourceDataInView_TogglesListVisibility()
    {
        var viewModel = CreateViewModel(new StubDataSourceRepository());
        await viewModel.LoadSourcesAsync();

        viewModel.SourceDataInViewIsExpanded.Should().BeTrue();
        viewModel.SourceDataInViewListVisibility.Should().Be(Visibility.Visible);

        viewModel.ToggleSourceDataInViewCommand.Execute(null);

        viewModel.SourceDataInViewIsExpanded.Should().BeFalse();
        viewModel.SourceDataInViewListVisibility.Should().Be(Visibility.Collapsed);

        viewModel.ToggleSourceDataInViewCommand.Execute(null);

        viewModel.SourceDataInViewIsExpanded.Should().BeTrue();
        viewModel.SourceDataInViewListVisibility.Should().Be(Visibility.Visible);
    }

    [Fact]
    public async Task OtherSourcesHeader_ShowsCount()
    {
        var repository = new StubDataSourceRepository
        {
            Sources =
            [
                new DataSource { DataSourceId = 7, SourceKey = "toggl_sleep", DisplayName = "Toggl Sleep" },
                new DataSource { DataSourceId = 8, SourceKey = "oura", DisplayName = "Oura" }
            ]
        };
        var cardProviderRegistry = new DataSourceCardProviderRegistry();
        cardProviderRegistry.Register(new StubCardProvider("toggl_sleep", hasData: true)
        {
            RangeData = [new DataSourceDayData(new DateOnly(2026, 05, 12), true, 1)]
        });
        var viewModel = CreateViewModel(repository, cardProviderRegistry: cardProviderRegistry);

        await viewModel.LoadSourcesAsync();

        viewModel.OtherSourcesHeader.Should().Be("Other data sources (1)");
    }

    [Fact]
    public async Task OtherSourcesSection_AlwaysVisibleWithZeroOtherItems()
    {
        var repository = new StubDataSourceRepository
        {
            Sources = [new DataSource { DataSourceId = 7, SourceKey = "toggl_sleep", DisplayName = "Toggl Sleep" }]
        };
        var cardProviderRegistry = new DataSourceCardProviderRegistry();
        cardProviderRegistry.Register(new StubCardProvider("toggl_sleep", hasData: true)
        {
            RangeData = [new DataSourceDayData(new DateOnly(2026, 05, 12), true, 1)]
        });
        var viewModel = CreateViewModel(repository, cardProviderRegistry: cardProviderRegistry);

        await viewModel.LoadSourcesAsync();

        viewModel.OtherSources.Should().BeEmpty();
        viewModel.OtherSourcesVisibility.Should().Be(Visibility.Visible);
        viewModel.OtherSourcesHeader.Should().Be("Other data sources (0)");
    }

    [Fact]
    public async Task ToggleOtherSources_TogglesListVisibility()
    {
        var viewModel = CreateViewModel(new StubDataSourceRepository());
        await viewModel.LoadSourcesAsync();

        viewModel.OtherSourcesIsExpanded.Should().BeTrue();
        viewModel.OtherSourcesListVisibility.Should().Be(Visibility.Visible);

        viewModel.ToggleOtherSourcesCommand.Execute(null);

        viewModel.OtherSourcesIsExpanded.Should().BeFalse();
        viewModel.OtherSourcesListVisibility.Should().Be(Visibility.Collapsed);

        viewModel.ToggleOtherSourcesCommand.Execute(null);

        viewModel.OtherSourcesIsExpanded.Should().BeTrue();
        viewModel.OtherSourcesListVisibility.Should().Be(Visibility.Visible);
    }

    public void Dispose()
    {
        WeakReferenceMessenger.Default.Reset();
    }

    private static DataSourcePanelViewModel CreateViewModel(
        StubDataSourceRepository repository,
        DataSourceImportHandlerRegistry? registry = null,
        ICalendarDaySelectionService? daySelectionService = null,
        TimeProvider? timeProvider = null,
        DataSourceCardProviderRegistry? cardProviderRegistry = null,
        ICalendarSelectionService? calendarSelectionService = null,
        IPendingEventDraftService? pendingEventDraftService = null,
        IEventRepository? eventRepository = null,
        IPendingEventRepository? pendingEventRepository = null,
        ICalendarViewRangeProvider? viewRangeProvider = null,
        StubSystemStateRepository? systemStateRepository = null)
    {
        return new DataSourcePanelViewModel(
            systemStateRepository ?? new StubSystemStateRepository(),
            repository,
            registry ?? new DataSourceImportHandlerRegistry(),
            daySelectionService ?? new StubCalendarDaySelectionService(),
            timeProvider ?? new FixedTimeProvider(new DateTimeOffset(2026, 05, 15, 12, 00, 00, TimeSpan.Zero)),
            cardProviderRegistry ?? new DataSourceCardProviderRegistry(),
            calendarSelectionService ?? new StubCalendarSelectionService(),
            pendingEventDraftService ?? new StubPendingEventDraftService(),
            pendingEventRepository ?? new StubPendingEventRepository(),
            viewRangeProvider ?? new StubCalendarViewRangeProvider(),
            eventRepository ?? new StubEventRepository());
    }

    private sealed class StubSystemStateRepository : ISystemStateRepository
    {
        public Dictionary<string, string> Values { get; } = [];

        public Task<string?> GetAsync(string key, CancellationToken ct = default)
        {
            Values.TryGetValue(key, out var value);
            return Task.FromResult<string?>(value);
        }

        public Task SetAsync(string key, string value, CancellationToken ct = default)
        {
            Values[key] = value;
            return Task.CompletedTask;
        }

        public Task SetManyAsync(IReadOnlyDictionary<string, string> pairs, CancellationToken ct = default)
        {
            foreach (var pair in pairs)
            {
                Values[pair.Key] = pair.Value;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class StubDataSourceRepository : IDataSourceRepository
    {
        private readonly List<TaskCompletionSource> _getAllSourcesWaiters = [];

        public IReadOnlyList<DataSource> Sources { get; set; } = [];

        public Dictionary<int, DataSourceImportLog?> LastImports { get; } = [];

        public Dictionary<(DateOnly Date, int DataSourceId), bool> Integrations { get; } = [];

        public List<(DateOnly Date, int DataSourceId, bool Integrated)> SetIntegrationCalls { get; } = [];

        public int GetAllSourcesCallCount { get; private set; }

        public Task<IReadOnlyList<DataSource>> GetAllSourcesAsync(CancellationToken ct = default)
        {
            GetAllSourcesCallCount++;
            foreach (var waiter in _getAllSourcesWaiters.ToArray())
            {
                waiter.TrySetResult();
            }

            return Task.FromResult(Sources);
        }

        public Task<DataSource?> GetSourceByKeyAsync(string sourceKey, CancellationToken ct = default)
            => Task.FromResult(Sources.SingleOrDefault(source => source.SourceKey == sourceKey));

        public Task<DataSource> UpsertSourceAsync(DataSource source, CancellationToken ct = default)
            => Task.FromResult(source);

        public Task<DateSourceIntegration?> GetIntegrationAsync(DateOnly date, int dataSourceId, CancellationToken ct = default)
        {
            if (!Integrations.TryGetValue((date, dataSourceId), out var integrated))
            {
                return Task.FromResult<DateSourceIntegration?>(null);
            }

            return Task.FromResult<DateSourceIntegration?>(new DateSourceIntegration
            {
                Date = date,
                DataSourceId = dataSourceId,
                Integrated = integrated
            });
        }

        public Task<DateSourceIntegration> SetIntegrationAsync(DateOnly date, int dataSourceId, bool integrated, CancellationToken ct = default)
        {
            SetIntegrationCalls.Add((date, dataSourceId, integrated));
            Integrations[(date, dataSourceId)] = integrated;
            return Task.FromResult(new DateSourceIntegration
            {
                Date = date,
                DataSourceId = dataSourceId,
                Integrated = integrated
            });
        }

        public Task<DataSourceImportLog?> GetLastImportAsync(int dataSourceId, CancellationToken ct = default)
        {
            LastImports.TryGetValue(dataSourceId, out var log);
            return Task.FromResult(log);
        }

        public Task<DataSourceImportLog> AddImportLogAsync(DataSourceImportLog log, CancellationToken ct = default)
            => Task.FromResult(log);

        public Task UpdateSourceColorAsync(int dataSourceId, string? colorHex, CancellationToken ct = default)
            => Task.CompletedTask;

        public async Task WaitForGetAllSourcesCallsAsync(int expectedCallCount)
        {
            while (GetAllSourcesCallCount < expectedCallCount)
            {
                var waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _getAllSourcesWaiters.Add(waiter);
                await waiter.Task;
            }
        }
    }

    private sealed class StubCalendarDaySelectionService : ICalendarDaySelectionService
    {
        public DateOnly? SelectedDay { get; set; }
        public DateOnly? ManuallySelectedDay { get; set; }
        public void SelectDay(DateOnly date) => SelectedDay = date;
        public void AutoSelectDay(DateOnly date) => SelectedDay = date;
        public void RestoreManualSelection() => SelectedDay = ManuallySelectedDay;
        public void ClearSelection() => SelectedDay = null;
    }

    private sealed class StubCardProvider : IDataSourceCardProvider, IDataSourceCardProviderPreloader, IDataSourceViewDataProvider, IDataSourceDayActionProvider
    {
        private readonly bool _hasData;

        public StubCardProvider(string sourceKey, bool hasData)
        {
            SourceKey = sourceKey;
            _hasData = hasData;
        }

        public string SourceKey { get; }

        public bool Preloaded { get; private set; }

        public IReadOnlyList<DataSourceDayData> RangeData { get; init; } = [];

        public Func<DateOnly, DateOnly, IReadOnlyList<DataSourceDayData>>? RangeDataFactory { get; init; }

        public bool SupportsAdd { get; init; }

        public List<DateOnly> AddedDates { get; } = [];

        public Task PreloadAsync(DateOnly date, CancellationToken ct = default)
        {
            Preloaded = true;
            return Task.CompletedTask;
        }

        public UIElement? CreateCompactSummaryView(DateOnly date) => null;

        public UIElement CreateDrilldownView(DateOnly date)
        {
            return DataSourceDayCardViewModel.CreatePlaceholderDrilldown(SourceKey);
        }

        public bool? HasDataForDay(DateOnly date) => Preloaded ? _hasData : null;

        public Task<IReadOnlyList<DataSourceDayData>> GetDataForRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
            => Task.FromResult(RangeDataFactory?.Invoke(from, to) ?? RangeData);

        public Task AddForDayAsync(DateOnly date, CancellationToken ct = default)
        {
            if (SupportsAdd)
            {
                AddedDates.Add(date);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class StubCalendarViewRangeProvider : ICalendarViewRangeProvider
    {
        public (DateOnly From, DateOnly To) Range { get; set; } =
            (new DateOnly(2026, 05, 11), new DateOnly(2026, 05, 17));

        public (DateOnly From, DateOnly To) GetCurrentViewDisplayRange() => Range;
    }

    private sealed class StubCalendarSelectionService : ICalendarSelectionService
    {
        public string? SelectedEventId { get; private set; }
        public CalendarEventSourceKind? SelectedSourceKind { get; private set; }
        public bool LastOpenInEditMode { get; private set; }

        public void Select(string eventId, CalendarEventSourceKind sourceKind, bool openInEditMode = false)
        {
            SelectedEventId = eventId;
            SelectedSourceKind = sourceKind;
            LastOpenInEditMode = openInEditMode;
        }

        public void ClearSelection()
        {
            SelectedEventId = null;
            SelectedSourceKind = null;
        }
    }

    private sealed class StubPendingEventDraftService : IPendingEventDraftService
    {
        public Task<Event> CreateDraftAsync(DateTime startLocal, DateTime endLocal, string? summary = null, CancellationToken ct = default)
            => Task.FromResult(new Event
            {
                EventId = "pending_new",
                Summary = summary,
                StartDatetime = startLocal,
                EndDatetime = endLocal
            });
    }

    private sealed class StubEventRepository : IEventRepository
    {
        public IList<Event> Events { get; set; } = [];

        public Task<Event?> GetByEventIdAsync(string eventId, CancellationToken ct = default)
            => Task.FromResult(Events.SingleOrDefault(e => e.EventId == eventId));

        public Task<Event?> GetByGcalEventIdAsync(string gcalEventId, CancellationToken ct = default)
            => Task.FromResult(Events.SingleOrDefault(e => e.GcalEventId == gcalEventId));

        public Task<IList<Event>> GetByDateRangeAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
            => Task.FromResult(Events);

        public Task<(DateOnly From, DateOnly To)?> GetStoredDateRangeAsync(CancellationToken ct = default)
            => Task.FromResult<(DateOnly From, DateOnly To)?>(null);

        public Task UpsertAsync(Event ev, CancellationToken ct = default)
        {
            Events = Events.Where(e => e.EventId != ev.EventId).Append(ev).ToList();
            return Task.CompletedTask;
        }

        public Task DeleteByEventIdAsync(string eventId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<Event?> GetDayNameEventAsync(DateOnly date, CancellationToken ct = default)
            => Task.FromResult<Event?>(null);

        public Task<Event?> GetSleepEventForDateAsync(DateOnly date, CancellationToken ct = default)
            => Task.FromResult<Event?>(null);
    }

    private sealed class StubPendingEventRepository : IPendingEventRepository
    {
        public PendingEvent? DayNameEvent { get; set; }

        public Task<PendingEvent?> GetByPendingEventIdAsync(string pendingEventId, CancellationToken ct = default)
            => Task.FromResult(DayNameEvent?.PendingEventId == pendingEventId ? DayNameEvent : null);

        public Task<PendingEvent?> GetByGcalEventIdAsync(string gcalEventId, CancellationToken ct = default)
            => Task.FromResult(DayNameEvent?.GcalEventId == gcalEventId ? DayNameEvent : null);

        public Task<PendingEvent?> GetDayNameEventAsync(DateOnly date, CancellationToken ct = default)
            => Task.FromResult(DayNameEvent);

        public Task UpsertAsync(PendingEvent pendingEvent, CancellationToken ct = default)
        {
            DayNameEvent = pendingEvent;
            return Task.CompletedTask;
        }

        public Task DeleteByPendingEventIdAsync(string pendingEventId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task DeleteByGcalEventIdAsync(string gcalEventId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<PendingEvent?> GetSleepEventForDateAsync(DateOnly date, CancellationToken ct = default)
            => Task.FromResult<PendingEvent?>(null);
    }

    private sealed class RecordingImportHandler : IDataSourceImportHandler
    {
        public RecordingImportHandler(string sourceKey)
        {
            SourceKey = sourceKey;
        }

        public string SourceKey { get; }

        public int TriggerCount { get; private set; }

        public Task TriggerImportAsync(CancellationToken ct = default)
        {
            TriggerCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
