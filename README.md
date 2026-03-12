# Adapter V2

Windows üzerinde çalışan TWAIN tarayıcı ile biyometrik/tarama akışlarını yöneten WinForms uygulaması (.NET 10).

## Çalışma şekli

- Uygulama açıldığında **sistem tepsisine** küçülür.

## Loglar nerede?

Tüm `ILogger` / Serilog çıktıları **metin dosyasına** yazılır:

| Ayar | Varsayılan | Açıklama |
|------|------------|----------|
| `Logging:LogDirectory` | `logs` | Göreli ise **exe’nin yanındaki** `logs` klasörüne çözülür. Mutlak yol da verilebilir. |
| Dosya adı | `kosmos-.txt` | Günlük rolling: `kosmos-20260305.txt`, `kosmos-20260306.txt` … |
| Saklama | `RetainedFileCountLimit` | Örn. 30 gün sonra eski dosyalar silinir. |

**Örnek tam yol (publish sonrası):**

```text
{KurulumKlasörü}\KosmosAdapterV2.exe
{KurulumKlasörü}\logs\kosmos-20260305.txt
```


## Diğer önemli klasörler (`appsettings.json`)

| Ayar | Varsayılan | Kullanım |
|------|------------|----------|
| `TempDirectory` | `C:\temp` | Geçici dosyalar |
| `ImageDirectory` | `C:\tmp\Images` | Foto isteği protokolü (`photo_request.txt`, `photo_{sessionId}.jpg`) |
| `FingerprintDirectory` | `C:\tmp\Fingerprints` | Parmak izi çıktıları |

## Protokol kaydı (kosmos2://)

Tarayıcıda “scheme does not have a registered handler” hatası alınıyorsa:

Kayıt **HKCU** altında yapılır; yönetici gerekmez.

## Yayınlama (publish)

```bash
dotnet publish KosmosAdapterV2\KosmosAdapterV2.csproj -c Release -r win-x86 --self-contained false
```

TWAIN sürücüleri için proje **x86** hedefler; publish çıktısını da **win-x86** ile almak genelde doğru olur.

## Gereksinimler

- Windows 10/11
- .NET 10 runtime (framework-dependent publish) veya self-contained paket
- Tarayıcı için uygun TWAIN/WIA sürücüleri (32-bit uyumu için x86 build)

## Lisans / sürüm

Uygulama içi “Hakkında” ve `version.local.json` / MinIO `version.json` ile sürüm takibi yapılabilir.
