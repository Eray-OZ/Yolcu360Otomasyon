using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Yolcu360Otomasyon.Models;
using Yolcu360Otomasyon.Services;

namespace Yolcu360Otomasyon;

public partial class MainWindow : Window
{
    private async void PaymentsTabButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_isAuthenticating) return;
        ShowPaymentsSection();
        await LoadPaymentsAsync();
    }

    private void CreatePaymentButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeUser is null || _selectedCollection is null)
        {
            HistoryStatusTextBlock.Text = "Ödeme oluşturmak için lütfen bir koleksiyon seçin.";
            return;
        }

        var vehicle = _selectedVehicle ?? _selectedCollectionVehicles.FirstOrDefault();
        if (vehicle is null)
        {
            HistoryStatusTextBlock.Text = "Ödeme yapmak için lütfen koleksiyon içerisinden bir araç seçin.";
            return;
        }

        CreatePaymentButton.IsEnabled = false;
        try
        {
            var vehiclePrice = ParseVehiclePrice(vehicle.Price);

            _paymentPreviewItems = new List<OdemeHazirlikItem>
            {
                new OdemeHazirlikItem
                {
                    KoleksiyonId = _selectedCollection.Id,
                    KoleksiyonAdi = $"{_selectedCollection.OzelAd} ({vehicle.Title})",
                    Tutar = vehiclePrice
                }
            };

            // Extra - Flight Car Recommendation START
            _lastPaidFlight = null;
            // Extra - Flight Car Recommendation END

            PrepareCheckoutSummary();
            ShowPaymentCheckoutSection();
        }
        catch (Exception ex)
        {
            HistoryStatusTextBlock.Text = $"Ödeme oluşturma hatası: {ex.Message}";
        }
        finally
        {
            CreatePaymentButton.IsEnabled = true;
        }
    }

    private enum PaymentFilterType
    {
        All,
        CarRental,
        Flight
    }

    private PaymentFilterType _currentPaymentFilter = PaymentFilterType.All;
    private List<OdemeListItem> _allPayments = new();

    private static decimal ParseVehiclePrice(string? priceText)
    {
        var parsed = DatabaseService.ParseCurrency(priceText ?? string.Empty);
        return parsed > 0 ? parsed : 100.00m;
    }

    private void BackFromCheckoutButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_paymentPreviewItems.Any(item => item.KoleksiyonAdi.StartsWith("[Uçak Bileti]")))
        {
            ShowFlightSection();
        }
        else
        {
            ShowHistorySection();
        }
    }

    private async void ConfirmPaymentButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_activeUser is null || _paymentPreviewItems.Count == 0)
        {
            CheckoutStatusTextBlock.Text = "Ödeme için seçili kayıt bulunamadı.";
            return;
        }

        ConfirmPaymentButton.IsEnabled = false;
        try
        {
            var paymentCard = BuildSandboxPaymentCardInput();
            CheckoutStatusTextBlock.Text = "iyzico sandbox ödeme isteği gönderiliyor...";

            var paymentResult = await _iyzicoPaymentService.CreateDirectPaymentAsync(
                _activeUser,
                _paymentPreviewItems,
                paymentCard);

            if (!string.Equals(paymentResult.Status, "success", StringComparison.OrdinalIgnoreCase))
            {
                CheckoutStatusTextBlock.Text =
                    $"Ödeme tamamlanmadı. Durum: {paymentResult.Status} / {paymentResult.PaymentStatus}. {paymentResult.ErrorMessage}";
                return;
            }

            await _databaseService.CreatePaymentsFromSandboxResultAsync(
                _activeUser.Id,
                _paymentPreviewItems,
                paymentResult);

            // Extra - Invoice PDF START
            try
            {
                foreach (var item in _paymentPreviewItems)
                {
                    var isFlight = item.KoleksiyonAdi.StartsWith("[Uçak Bileti]");
                    var total = item.Tutar;
                    var subtotal = Math.Round(total / 1.20m, 2);
                    var kdv = total - subtotal;

                    var invoiceModel = new InvoiceModel
                    {
                        FaturaNo = $"FAT-{paymentResult.ReferenceNo.Substring(Math.Max(0, paymentResult.ReferenceNo.Length - 8))}",
                        ReferansNo = paymentResult.ReferenceNo,
                        DuzenlemeTarihi = DateTime.Now,
                        MusteriEmail = _activeUser.Email,
                        MusteriTelefon = _activeUser.PhoneNumber,
                        KartSahibi = paymentResult.CardHolderName ?? _activeUser.Email,
                        KartSon4 = paymentResult.LastFourDigits ?? string.Empty,
                        OdemeSaglayici = paymentResult.Provider,
                        HizmetBasligi = item.KoleksiyonAdi,
                        HizmetTuru = isFlight ? "Uçak Bileti Rezervasyonu" : "Araç Kiralama Rezervasyonu",
                        AraToplam = subtotal,
                        KdvTutari = kdv,
                        GenelToplam = total,
                        ParaBirimi = "TRY",
                        OdemeDurumu = "SUCCESS"
                    };

                    _ = _invoicePdfService.GenerateInvoicePdfAsync(invoiceModel);
                }
            }
            catch {}
            // Extra - Invoice PDF END

            ClearCheckoutForm();

            // Extra - Flight Car Recommendation START
            if (_lastPaidFlight is not null)
            {
                PrepareFlightCarRecommendationView();
                ShowFlightCarRecommendationSection();
                return;
            }
            // Extra - Flight Car Recommendation END

            ShowPaymentsSection();
            await LoadPaymentsAsync();
            PaymentsStatusTextBlock.Text = "iyzico sandbox ödeme kaydı ve Fatura PDF'i oluşturuldu.";
        }
        catch (Exception ex)
        {
            CheckoutStatusTextBlock.Text = $"Ödeme hatası: {ex.Message}";
        }
        finally
        {
            ConfirmPaymentButton.IsEnabled = true;
        }
    }

    private async Task LoadPaymentsAsync()
    {
        if (_activeUser is null)
            return;

        _allPayments = await _databaseService.GetPaymentsAsync(_activeUser.Id);
        ApplyPaymentFilter();
    }

    private void ApplyPaymentFilter()
    {
        var filtered = _currentPaymentFilter switch
        {
            PaymentFilterType.CarRental => _allPayments.Where(p => !p.KoleksiyonAdi.StartsWith("[Uçak Bileti]")).ToList(),
            PaymentFilterType.Flight => _allPayments.Where(p => p.KoleksiyonAdi.StartsWith("[Uçak Bileti]")).ToList(),
            _ => _allPayments
        };

        PaymentsDataGrid.ItemsSource = null;
        PaymentsDataGrid.ItemsSource = filtered;

        UpdatePaymentFilterButtonStyles();

        var filterName = _currentPaymentFilter switch
        {
            PaymentFilterType.CarRental => "araç kiralama",
            PaymentFilterType.Flight => "uçak bileti",
            _ => "tüm"
        };

        PaymentsStatusTextBlock.Text = filtered.Count == 0
            ? $"{filterName} ödeme kaydı bulunamadı."
            : $"{filtered.Count} {filterName} ödeme kaydı listelendi (Toplam {_allPayments.Count} kayıt). Faturayı görüntülemek için çift tıklayın.";
    }

    private void UpdatePaymentFilterButtonStyles()
    {
        PaymentFilterAllButton.Classes.Set("primary", _currentPaymentFilter == PaymentFilterType.All);
        PaymentFilterAllButton.Classes.Set("btn-secondary", _currentPaymentFilter != PaymentFilterType.All);

        PaymentFilterCarButton.Classes.Set("primary", _currentPaymentFilter == PaymentFilterType.CarRental);
        PaymentFilterCarButton.Classes.Set("btn-secondary", _currentPaymentFilter != PaymentFilterType.CarRental);

        PaymentFilterFlightButton.Classes.Set("primary", _currentPaymentFilter == PaymentFilterType.Flight);
        PaymentFilterFlightButton.Classes.Set("btn-secondary", _currentPaymentFilter != PaymentFilterType.Flight);
    }

    private void PaymentFilterAllButton_Click(object? sender, RoutedEventArgs e)
    {
        _currentPaymentFilter = PaymentFilterType.All;
        ApplyPaymentFilter();
    }

    private void PaymentFilterCarButton_Click(object? sender, RoutedEventArgs e)
    {
        _currentPaymentFilter = PaymentFilterType.CarRental;
        ApplyPaymentFilter();
    }

    private void PaymentFilterFlightButton_Click(object? sender, RoutedEventArgs e)
    {
        _currentPaymentFilter = PaymentFilterType.Flight;
        ApplyPaymentFilter();
    }

    // Extra - Invoice PDF START
    private async void OpenInvoicePdfButton_Click(object? sender, RoutedEventArgs e)
    {
        if (PaymentsDataGrid.SelectedItem is not OdemeListItem selectedOdeme)
        {
            PaymentsStatusTextBlock.Text = "Faturasını görüntülemek için lütfen listeden bir ödeme seçin.";
            return;
        }

        await OpenInvoiceViewerAsync(selectedOdeme);
    }

    private async void PaymentsDataGrid_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (PaymentsDataGrid.SelectedItem is not OdemeListItem selectedOdeme)
            return;

        await OpenInvoiceViewerAsync(selectedOdeme);
    }

    private async Task OpenInvoiceViewerAsync(OdemeListItem odeme)
    {
        if (_activeUser is null) return;

        PaymentsStatusTextBlock.Text = $"{odeme.ReferansNo} numaralı ödemenin faturası hazırlanıyor...";
        try
        {
            var downloadsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads");

            var safeRef = string.Concat(odeme.ReferansNo.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_'));
            var pdfPath = Path.Combine(downloadsDir, $"Fatura_{safeRef}.pdf");

            if (!File.Exists(pdfPath))
            {
                var isFlight = odeme.KoleksiyonAdi.StartsWith("[Uçak Bileti]");
                var total = odeme.Tutar;
                var subtotal = Math.Round(total / 1.20m, 2);
                var kdv = total - subtotal;

                var invoiceModel = new InvoiceModel
                {
                    FaturaNo = $"FAT-{odeme.ReferansNo.Substring(Math.Max(0, odeme.ReferansNo.Length - 8))}",
                    ReferansNo = odeme.ReferansNo,
                    DuzenlemeTarihi = odeme.OdemeTarihi,
                    MusteriEmail = _activeUser.Email,
                    MusteriTelefon = _activeUser.PhoneNumber,
                    KartSahibi = odeme.KartSahibi ?? _activeUser.Email,
                    KartSon4 = odeme.KartSon4 ?? string.Empty,
                    OdemeSaglayici = odeme.Saglayici,
                    HizmetBasligi = odeme.KoleksiyonAdi,
                    HizmetTuru = isFlight ? "Uçak Bileti Rezervasyonu" : "Araç Kiralama Rezervasyonu",
                    AraToplam = subtotal,
                    KdvTutari = kdv,
                    GenelToplam = total,
                    ParaBirimi = odeme.ParaBirimi,
                    OdemeDurumu = odeme.Durum
                };

                pdfPath = await _invoicePdfService.GenerateInvoicePdfAsync(invoiceModel, downloadsDir);
            }

            _currentOpenedInvoicePdfPath = pdfPath;

            InvoiceInfoRefTextBlock.Text = $"Referans: {odeme.ReferansNo}";
            InvoiceInfoPathTextBlock.Text = $"Dosya: {Path.GetFileName(pdfPath)}";
            InvoiceInfoAmountTextBlock.Text = $"Tutar: {odeme.Tutar:N2} {odeme.ParaBirimi}";

            InvoicePdfWebView.Source = new Uri($"file://{pdfPath}");

            ShowInvoiceViewerSection();
        }
        catch (Exception ex)
        {
            PaymentsStatusTextBlock.Text = $"Fatura görüntüleme hatası: {ex.Message}";
        }
    }

    private void OpenExternalPdfButton_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentOpenedInvoicePdfPath) || !File.Exists(_currentOpenedInvoicePdfPath))
            return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _currentOpenedInvoicePdfPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Invoice] Harici açma hatası: {ex.Message}");
        }
    }

    private void OpenInvoiceFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentOpenedInvoicePdfPath) || !File.Exists(_currentOpenedInvoicePdfPath))
            return;

        try
        {
            System.Diagnostics.Process.Start("open", $"-R \"{_currentOpenedInvoicePdfPath}\"");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Invoice] Klasörde gösterme hatası: {ex.Message}");
        }
    }

    private void CloseInvoiceViewerButton_Click(object? sender, RoutedEventArgs e)
    {
        ShowPaymentsSection();
    }
    // Extra - Invoice PDF END

    private void PrepareCheckoutSummary()
    {
        var trCulture = new System.Globalization.CultureInfo("tr-TR");
        var total = _paymentPreviewItems.Sum(item => item.Tutar);
        PaymentSummaryCollectionsTextBlock.Text = string.Join(Environment.NewLine, _paymentPreviewItems.Select(item =>
            $"{item.KoleksiyonAdi} - {item.Tutar.ToString("N2", trCulture)} TL"));
        PaymentSummaryCountTextBlock.Text = $"{_paymentPreviewItems.Count} kayıt seçildi";
        PaymentSummaryTotalTextBlock.Text = $"{total.ToString("N2", trCulture)} TL";
        CheckoutStatusTextBlock.Text = "Ödeme iyzico sandbox sayfasında tamamlanacak.";
    }

    private void ClearCheckoutForm()
    {
        PaymentCardHolderTextBox.Text = string.Empty;
        PaymentCardNumberTextBox.Text = string.Empty;
        PaymentExpiryMonthTextBox.Text = string.Empty;
        PaymentExpiryYearTextBox.Text = string.Empty;
        PaymentCvvTextBox.Text = string.Empty;
        _paymentPreviewItems = new List<OdemeHazirlikItem>();
    }

    private SandboxPaymentCardInput BuildSandboxPaymentCardInput()
    {
        var cardHolderName = PaymentCardHolderTextBox.Text?.Trim() ?? string.Empty;
        var cardNumber = PaymentCardNumberTextBox.Text?.Trim() ?? string.Empty;
        var expiryMonth = PaymentExpiryMonthTextBox.Text?.Trim() ?? string.Empty;
        var expiryYear = PaymentExpiryYearTextBox.Text?.Trim() ?? string.Empty;
        var cvc = PaymentCvvTextBox.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(cardHolderName) ||
            string.IsNullOrWhiteSpace(cardNumber) ||
            string.IsNullOrWhiteSpace(expiryMonth) ||
            string.IsNullOrWhiteSpace(expiryYear) ||
            string.IsNullOrWhiteSpace(cvc))
        {
            throw new InvalidOperationException("Test kartı alanlarının tamamı zorunlu.");
        }

        return new SandboxPaymentCardInput
        {
            CardHolderName = cardHolderName,
            CardNumber = cardNumber,
            ExpiryMonth = expiryMonth,
            ExpiryYear = expiryYear,
            Cvc = cvc
        };
    }
}
