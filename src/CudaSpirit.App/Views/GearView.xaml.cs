using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CudaSpirit.App.Infra;
using CudaSpirit.Core.Models;

namespace CudaSpirit.App.Views;

public partial class GearView : UserControl, IRefreshable
{
    private readonly ServiceHub _hub = ServiceHub.Instance;

    public GearView()
    {
        InitializeComponent();
        SlotBox.ItemsSource = Enum.GetValues(typeof(GearSlot));
        KindBox.ItemsSource = Enum.GetValues(typeof(EnhanceKind));
        GradeBox.ItemsSource = Enum.GetValues(typeof(EnhanceGrade));
        SlotBox.SelectedIndex = 0;
        KindBox.SelectedIndex = 0;
        GradeBox.SelectedItem = EnhanceGrade.PEN;
        Refresh();
    }

    public void Refresh()
    {
        GearList.ItemsSource = _hub.Db.GetGear().Select(g => new
        {
            g.Id,
            g.Slot,
            Display = g.ToString(),
            ApText = g.Ap > 0 ? $"{g.Ap} AP" : "",
            DpText = g.Dp > 0 ? $"{g.Dp} DP" : "",
            CaphrasText = g.Caphras > 0 ? $"Caphras {g.Caphras}" : ""
        }).ToList();
    }

    private void OnAdd(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text)) return;
        var item = new GearItem
        {
            Name = NameBox.Text.Trim(),
            Slot = (GearSlot)(SlotBox.SelectedItem ?? GearSlot.Other),
            Kind = (EnhanceKind)(KindBox.SelectedItem ?? EnhanceKind.Weapon),
            Grade = (EnhanceGrade)(GradeBox.SelectedItem ?? EnhanceGrade.Base),
            Ap = ParseInt(ApBox.Text),
            Dp = ParseInt(DpBox.Text),
            Caphras = ParseInt(CaphrasBox.Text),
            Equipped = EquippedBox.IsChecked == true
        };
        _hub.Db.UpsertGear(item);
        _hub.Live.RefreshGear();
        NameBox.Clear(); ApBox.Clear(); DpBox.Clear(); CaphrasBox.Clear();
        Refresh();
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: long id })
        {
            _hub.Db.DeleteGear(id);
            _hub.Live.RefreshGear();
            Refresh();
        }
    }

    private static int ParseInt(string s) =>
        int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
}
