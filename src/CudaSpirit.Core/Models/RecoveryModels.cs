using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CudaSpirit.Core.Models;

public sealed class ReturnerProfile
{
    public int MonthsAway { get; set; } = 6;
    public bool InventoryIsChaotic { get; set; } = true;
    public bool UnsureAboutCurrentGear { get; set; } = true;
    public bool UnsureAboutMainClass { get; set; }
    public bool WantsFreshCharacter { get; set; }
    public bool HasUnclaimedRewards { get; set; } = true;
    public bool HasSeasonCharacter { get; set; }
    public bool WantsToSpendPearls { get; set; }
    public string Goal { get; set; } = "Relearn the game safely";
}

public sealed class RecoveryStep
{
    public int Order { get; set; }
    public string Phase { get; set; } = "";
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
    public string Why { get; set; } = "";
    public bool Critical { get; set; }
    public string ToolRoute { get; set; } = "";
}

public sealed class ReturnerPlan
{
    public string Headline { get; set; } = "";
    public string Summary { get; set; } = "";
    public IReadOnlyList<RecoveryStep> Steps { get; set; } = Array.Empty<RecoveryStep>();
}

public sealed class RetirementCheckItem : INotifyPropertyChanged
{
    private bool _isChecked;

    public string Key { get; set; } = "";
    public string Category { get; set; } = "";
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
    public string Verification { get; set; } = "";
    public bool Critical { get; set; }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value) return;
            _isChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class RetirementAssessment
{
    public int Completed { get; set; }
    public int Total { get; set; }
    public int CriticalRemaining { get; set; }
    public int ReadinessPercent { get; set; }
    public bool SafeToDelete { get; set; }
    public string Verdict { get; set; } = "";
    public IReadOnlyList<string> Blockers { get; set; } = Array.Empty<string>();
}

public sealed class ClassPreferenceInput
{
    public string Range { get; set; } = "Any";
    public string Pace { get; set; } = "Balanced";
    public string Complexity { get; set; } = "Moderate";
    public string Survivability { get; set; } = "Balanced";
    public string Focus { get; set; } = "PvE";
    public bool WantsSupport { get; set; }
    public bool WantsGrab { get; set; }
    public bool AvoidsHighApm { get; set; }
}

public sealed class ClassProfile
{
    public string Name { get; set; } = "";
    public string Range { get; set; } = "Melee";
    public string Pace { get; set; } = "Balanced";
    public int Complexity { get; set; } = 3;
    public int Survivability { get; set; } = 3;
    public int Mobility { get; set; } = 3;
    public int PvE { get; set; } = 3;
    public int PvP { get; set; } = 3;
    public int Support { get; set; }
    public bool HasGrab { get; set; }
    public string Identity { get; set; } = "";
    public string Caveat { get; set; } = "";
}

public sealed class ClassRecommendation
{
    public string ClassName { get; set; } = "";
    public int MatchPercent { get; set; }
    public string Why { get; set; } = "";
    public string WatchOutFor { get; set; } = "";
    public string TrialPlan { get; set; } = "";
}
