using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoogleCalendarManagement.Data.Entities;
using GoogleCalendarManagement.Services;
using Microsoft.UI.Xaml;

namespace GoogleCalendarManagement.ViewModels;

public sealed class TogglPhoneRulesViewModel : ObservableObject
{
    private readonly ITogglPhoneRuleRepository _ruleRepository;
    private readonly ITogglPhoneClassificationService _classificationService;

    private bool _isLoading;
    private bool _isReclassifying;
    private string _statusMessage = "";

    public TogglPhoneRulesViewModel(
        ITogglPhoneRuleRepository ruleRepository,
        ITogglPhoneClassificationService classificationService)
    {
        _ruleRepository = ruleRepository;
        _classificationService = classificationService;

        AddRuleCommand = new AsyncRelayCommand(AddRuleAsync);
        ReclassifyAllCommand = new AsyncRelayCommand(ReclassifyAllAsync, () => !IsReclassifying);
    }

    public ObservableCollection<TogglPhoneRuleItemViewModel> Rules { get; } = [];

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(LoadingVisibility));
            }
        }
    }

    public bool IsReclassifying
    {
        get => _isReclassifying;
        private set
        {
            if (SetProperty(ref _isReclassifying, value))
            {
                ReclassifyAllCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(ReclassifyingVisibility));
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(StatusVisibility));
            }
        }
    }

    public Visibility LoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ReclassifyingVisibility => IsReclassifying ? Visibility.Visible : Visibility.Collapsed;
    public Visibility StatusVisibility => string.IsNullOrEmpty(StatusMessage) ? Visibility.Collapsed : Visibility.Visible;

    public IAsyncRelayCommand AddRuleCommand { get; }
    public IAsyncRelayCommand ReclassifyAllCommand { get; }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        try
        {
            var rules = await _ruleRepository.GetAllRulesAsync(ct);
            Rules.Clear();
            foreach (var rule in rules)
            {
                Rules.Add(new TogglPhoneRuleItemViewModel(rule, _ruleRepository, this));
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    internal async Task RefreshAsync(CancellationToken ct = default)
    {
        await LoadAsync(ct);
    }

    private async Task AddRuleAsync()
    {
        var newRule = new TogglPhoneRule
        {
            DescriptionPattern = "NewRule",
            MaxDurationMinutes = 10,
            IsActive = true
        };

        await _ruleRepository.AddRuleAsync(newRule);
        await LoadAsync();
    }

    private async Task ReclassifyAllAsync()
    {
        IsReclassifying = true;
        StatusMessage = "";
        try
        {
            await _classificationService.ClassifyAllAsync();
            StatusMessage = "Re-classification complete.";
        }
        catch (Exception)
        {
            StatusMessage = "Re-classification failed. Check the logs.";
        }
        finally
        {
            IsReclassifying = false;
        }
    }
}

public sealed class TogglPhoneRuleItemViewModel : ObservableObject
{
    private readonly ITogglPhoneRuleRepository _repository;
    private readonly TogglPhoneRulesViewModel _parent;

    private string _descriptionPattern;
    private int? _maxDurationMinutes;
    private bool _isActive;
    private string? _notes;
    private DateOnly? _dateFrom;
    private DateOnly? _dateTo;

    public TogglPhoneRuleItemViewModel(TogglPhoneRule rule, ITogglPhoneRuleRepository repository, TogglPhoneRulesViewModel parent)
    {
        _repository = repository;
        _parent = parent;
        Id = rule.Id;
        _descriptionPattern = rule.DescriptionPattern;
        _maxDurationMinutes = rule.MaxDurationMinutes;
        _isActive = rule.IsActive;
        _notes = rule.Notes;
        _dateFrom = rule.DateFrom;
        _dateTo = rule.DateTo;

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        DeactivateCommand = new AsyncRelayCommand(DeactivateAsync, () => IsActive);
    }

    public int Id { get; }

    public string DescriptionPattern
    {
        get => _descriptionPattern;
        set => SetProperty(ref _descriptionPattern, value);
    }

    public int? MaxDurationMinutes
    {
        get => _maxDurationMinutes;
        set => SetProperty(ref _maxDurationMinutes, value);
    }

    public string MaxDurationMinutesText
    {
        get => _maxDurationMinutes?.ToString() ?? "";
        set
        {
            if (int.TryParse(value, out var parsed) && parsed > 0)
            {
                MaxDurationMinutes = parsed;
            }
            else if (string.IsNullOrWhiteSpace(value))
            {
                MaxDurationMinutes = null;
            }
        }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (SetProperty(ref _isActive, value))
            {
                DeactivateCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string? Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public DateOnly? DateFrom
    {
        get => _dateFrom;
        set => SetProperty(ref _dateFrom, value);
    }

    public DateOnly? DateTo
    {
        get => _dateTo;
        set => SetProperty(ref _dateTo, value);
    }

    public string DateRangeLabel =>
        (_dateFrom, _dateTo) switch
        {
            (null, null) => "All dates",
            ({ } from, null) => $"From {from:yyyy-MM-dd}",
            (null, { } to) => $"Until {to:yyyy-MM-dd}",
            ({ } from, { } to) => $"{from:yyyy-MM-dd} – {to:yyyy-MM-dd}"
        };

    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand DeactivateCommand { get; }

    private async Task SaveAsync()
    {
        var rule = new TogglPhoneRule
        {
            Id = Id,
            DescriptionPattern = DescriptionPattern,
            MaxDurationMinutes = MaxDurationMinutes,
            IsActive = IsActive,
            Notes = Notes,
            DateFrom = DateFrom,
            DateTo = DateTo
        };

        await _repository.UpdateRuleAsync(rule);
        await _parent.RefreshAsync();
    }

    private async Task DeactivateAsync()
    {
        await _repository.DeactivateRuleAsync(Id);
        IsActive = false;
        await _parent.RefreshAsync();
    }
}
