using Yolcu360Otomasyon.Models;

namespace Yolcu360Otomasyon;

public partial class MainWindow
{
    private void PrepareCheckoutSummary()
    {
        var trCulture = new System.Globalization.CultureInfo("tr-TR");
        var total = _paymentPreviewItems.Sum(item => item.Tutar);
        PaymentSummaryCollectionsTextBlockControl.Text = string.Join(Environment.NewLine, _paymentPreviewItems.Select(item =>
            $"{item.KoleksiyonAdi} - {item.Tutar.ToString("N2", trCulture)} TL"));
        PaymentSummaryCountTextBlockControl.Text = $"{_paymentPreviewItems.Count} kayıt seçildi";
        PaymentSummaryTotalTextBlockControl.Text = $"{total.ToString("N2", trCulture)} TL";
        SetCheckoutStatus("Ödeme iyzico sandbox sayfasında tamamlanacak.");
    }

    private void ClearCheckoutForm()
    {
        PaymentCardHolderTextBoxControl.Text = string.Empty;
        PaymentCardNumberTextBoxControl.Text = string.Empty;
        PaymentExpiryMonthTextBoxControl.Text = string.Empty;
        PaymentExpiryYearTextBoxControl.Text = string.Empty;
        PaymentCvvTextBoxControl.Text = string.Empty;
        _paymentPreviewItems = new List<OdemeHazirlikItem>();
    }
}
