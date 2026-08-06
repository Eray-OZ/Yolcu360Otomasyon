using Avalonia.Controls;
using Avalonia.Data;
using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon;

public partial class MainWindow : Window
{
    private void ShowSearchSection()
    {
        SearchPanel.IsVisible = true;
        SearchResultsPanel.IsVisible = true;
        HistoryPanel.IsVisible = false;
        PaymentsPanel.IsVisible = false;
        PaymentCheckoutPanel.IsVisible = false;
        SearchTabButton.Classes.Set("primary", true);
        HistoryTabButton.Classes.Set("primary", false);
        PaymentsTabButton.Classes.Set("primary", false);
    }

    private void ShowHistorySection()
    {
        SearchPanel.IsVisible = false;
        SearchResultsPanel.IsVisible = false;
        HistoryPanel.IsVisible = true;
        PaymentsPanel.IsVisible = false;
        PaymentCheckoutPanel.IsVisible = false;
        SearchTabButton.Classes.Set("primary", false);
        HistoryTabButton.Classes.Set("primary", true);
        PaymentsTabButton.Classes.Set("primary", false);
    }

    private void ShowPaymentsSection()
    {
        SearchPanel.IsVisible = false;
        SearchResultsPanel.IsVisible = false;
        HistoryPanel.IsVisible = false;
        PaymentsPanel.IsVisible = true;
        PaymentCheckoutPanel.IsVisible = false;
        SearchTabButton.Classes.Set("primary", false);
        HistoryTabButton.Classes.Set("primary", false);
        PaymentsTabButton.Classes.Set("primary", true);
    }

    private void ShowPaymentCheckoutSection()
    {
        SearchPanel.IsVisible = false;
        SearchResultsPanel.IsVisible = false;
        HistoryPanel.IsVisible = false;
        PaymentsPanel.IsVisible = false;
        PaymentCheckoutPanel.IsVisible = true;
        SearchTabButton.Classes.Set("primary", false);
        HistoryTabButton.Classes.Set("primary", false);
        PaymentsTabButton.Classes.Set("primary", true);
    }

    private void ConfigureResultsGrid()
    {
        ResultsDataGrid.AutoGenerateColumns = false;
        ResultsDataGrid.Columns.Clear();

        ResultsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Araç",
            Binding = new Binding(nameof(SearchResultItem.Title)),
            Width = new DataGridLength(2, DataGridLengthUnitType.Star)
        });

        ResultsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Detay",
            Binding = new Binding(nameof(SearchResultItem.Subtitle)),
            Width = new DataGridLength(2, DataGridLengthUnitType.Star)
        });

        ResultsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Toplam Fiyat",
            Binding = new Binding(nameof(SearchResultItem.Price)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });

        ResultsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Günlük",
            Binding = new Binding(nameof(SearchResultItem.DailyPrice)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });

        ResultsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Vites",
            Binding = new Binding(nameof(SearchResultItem.Transmission)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });

        ResultsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Yakıt",
            Binding = new Binding(nameof(SearchResultItem.FuelType)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });

        ResultsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Şirket",
            Binding = new Binding(nameof(SearchResultItem.Supplier)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });

        ResultsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Teslim",
            Binding = new Binding(nameof(SearchResultItem.PickupInfo)),
            Width = new DataGridLength(2, DataGridLengthUnitType.Star)
        });
    }

    private void ConfigureCollectionsGrid()
    {
        CollectionsDataGrid.AutoGenerateColumns = false;
        CollectionsDataGrid.Columns.Clear();

        CollectionsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Kayıt Adı",
            Binding = new Binding(nameof(KoleksiyonListItem.OzelAd)),
            Width = new DataGridLength(1.8, DataGridLengthUnitType.Star)
        });

        CollectionsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Alış Yeri",
            Binding = new Binding(nameof(KoleksiyonListItem.AlisYeri)),
            Width = new DataGridLength(1.4, DataGridLengthUnitType.Star)
        });

        CollectionsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Tarih Aralığı",
            Binding = new Binding(nameof(KoleksiyonListItem.AlisTarihi))
            {
                StringFormat = "dd.MM.yyyy"
            },
            Width = new DataGridLength(1.1, DataGridLengthUnitType.Star)
        });

        CollectionsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Dönüş",
            Binding = new Binding(nameof(KoleksiyonListItem.DonusTarihi))
            {
                StringFormat = "dd.MM.yyyy"
            },
            Width = new DataGridLength(1.1, DataGridLengthUnitType.Star)
        });

        CollectionsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Vites",
            Binding = new Binding(nameof(KoleksiyonListItem.SecilenVitesFiltresi)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });

        CollectionsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Yakıt",
            Binding = new Binding(nameof(KoleksiyonListItem.SecilenYakitFiltresi)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });

        CollectionsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Araç Sayısı",
            Binding = new Binding(nameof(KoleksiyonListItem.AracSayisi)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });

        CollectionsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Tarih",
            Binding = new Binding(nameof(KoleksiyonListItem.OlusturmaTarihi))
            {
                StringFormat = "dd.MM.yyyy HH:mm"
            },
            Width = new DataGridLength(1.4, DataGridLengthUnitType.Star)
        });
    }

    private void ConfigurePaymentsGrid()
    {
        PaymentsDataGrid.AutoGenerateColumns = false;
        PaymentsDataGrid.Columns.Clear();

        PaymentsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Referans",
            Binding = new Binding(nameof(OdemeListItem.ReferansNo)),
            Width = new DataGridLength(1.6, DataGridLengthUnitType.Star)
        });

        PaymentsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Kayıt",
            Binding = new Binding(nameof(OdemeListItem.KoleksiyonAdi)),
            Width = new DataGridLength(1.8, DataGridLengthUnitType.Star)
        });

        PaymentsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Tutar",
            Binding = new Binding(nameof(OdemeListItem.Tutar))
            {
                StringFormat = "N2"
            },
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });

        PaymentsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "PB",
            Binding = new Binding(nameof(OdemeListItem.ParaBirimi)),
            Width = new DataGridLength(0.7, DataGridLengthUnitType.Star)
        });

        PaymentsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Durum",
            Binding = new Binding(nameof(OdemeListItem.Durum)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });

        PaymentsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Sağlayıcı",
            Binding = new Binding(nameof(OdemeListItem.Saglayici)),
            Width = new DataGridLength(1.1, DataGridLengthUnitType.Star)
        });

        PaymentsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Kart",
            Binding = new Binding(nameof(OdemeListItem.KartSon4)),
            Width = new DataGridLength(0.8, DataGridLengthUnitType.Star)
        });

        PaymentsDataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Tarih",
            Binding = new Binding(nameof(OdemeListItem.OdemeTarihi))
            {
                StringFormat = "dd.MM.yyyy HH:mm"
            },
            Width = new DataGridLength(1.3, DataGridLengthUnitType.Star)
        });
    }

    private static string NormalizeDigits(string? value)
    {
        return new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
    }
}
