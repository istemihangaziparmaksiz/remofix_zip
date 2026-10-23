<div align="center">

<img src="Assets/R.png" width="96" height="96" alt="Remofix Logo" />

# Remofix Zip
### Remote Archive Inspector & Selective Downloader

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-0078D6?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/istemihangaziparmaksiz/remofix_zip)
[![Framework](https://img.shields.io/badge/.NET-8.0%20WPF-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Architecture](https://img.shields.io/badge/Arch-x64%20Standalone-10B981?style=for-the-badge)](https://github.com/istemihangaziparmaksiz/remofix_zip/releases)
[![Status](https://img.shields.io/badge/Status-Stable%20v1.0.0-success?style=for-the-badge)]()

**Remofix Zip**, internet üzerindeki onlarca gigabaytlık büyük ZIP arşivlerinin **tamamını indirmeye gerek kalmadan**, **HTTP Range (206 Partial Content)** teknolojisi ile doğrudan dosya sonundaki *Central Directory* yapısını saniyeler içinde okuyan ve arşiv içinden sadece dilediğiniz dosyaları seçerek indirmenizi sağlayan yeni nesil Windows masaüstü aracıdır.

[📥 Son Sürümü İndir (v1.0.0)](https://github.com/istemihangaziparmaksiz/remofix_zip/releases/latest) • [✨ Özellikler](#-öne-çıkan-özellikler) • [⚙️ Mimari ve Çalışma Mantığı](#️-nasıl-çalışır) • [🚀 Kurulum ve Kullanım](#-kurulum-ve-kullanım)

---

</div>

## 💡 Neden Remofix Zip?

Çoğu zaman 10 GB - 50 GB boyutundaki bir ROM, yazılım paketi veya veri arşivi içinden yalnızca 5 MB'lık tek bir dosyaya (örneğin bir `boot.img`, `payload.bin`, `config.json` veya `firmware` dosyasına) ihtiyaç duyarız. Standart tarayıcılar veya indirme yöneticileri dosyanın tamamını indirmenizi zorunlu kılar; bu da ciddi bir zaman, disk alanı ve internet kotası kaybına neden olur.

**Remofix Zip**, ZIP dosya biçiminin mimari yapısını kullanarak dosyanın sadece son birkaç kilobaytlık dizin indeksini çeker. Arşivin tüm klasör ve dosya ağacını ekrana getirir ve yalnızca işaretlediğiniz dosyaları indirir.

---

## ✨ Öne Çıkan Özellikler

### 1. ⚡ Tam ZIP64 ve Dev Dosya Desteği
* 4 GB sınırını aşan devasa arşivleri ve 65.535 adetten fazla dosya barındıran ZIP64 arşivlerini sorunsuz bir şekilde ayrıştırır ve indirir.

### 2. 🔐 Akıllı Parola Yönetimi (AES-256 & ZipCrypto)
* Şifrelenmiş ZIP arşivlerinde indirme başlatılırken şık bir parola penceresi açılır.
* Girilen parola **URL oturumu boyunca hafızada tutulur**; arşivden seçilen onlarca dosya için her defasında tekrar tekrar parola sormaz.

### 3. 🌐 Dahili Chromium Web Tarayıcısı (WebView2 Sniffer)
* Program içerisinde gömülü tam teşekküllü modern bir tarayıcı yer alır.
* Sitelerde gezinirken tıklandığında veya yeni sekmede açıldığında bir `.zip` dosyası indirmesi tetiklenirse:
  - Tarayıcının standart indirmesi durdurulur,
  - Ekrana *"ZIP dosyası tespit edildi, incelensin mi?"* onayı gelir,
  - Onay verildiğinde otomatik olarak **ZIP Gezgini** sekmesine geçilerek doğrudan analiz başlatılır.
  - Tarayıcı çerezleri (`Cookies`) otomatik aktarıldığı için üyelik veya oturum gerektiren korumalı indirme bağlantılarında da sorunsuz çalışır.

### 4. 🌳 Hiyerarşik Klasör Ağacı (TreeView & Multi-Select)
* Windows Gezgini yapısında klasör ve dosya ağacı.
* **Üç Durumlu Onay Kutuları:** Bir üst klasör seçildiğinde altındaki tüm dosya ve alt dizinler otomatik olarak seçilir.
* Dosya boyutu, sıkıştırılmış boyut, sıkıştırma türü ve tasarruf oranı her dosyanın yanında rozet olarak gösterilir.

### 5. 🚀 Çok Kanallı Parçalı İndirme Motoru
* HTTP Range istekleri üzerinden büyük dosyaları eşzamanlı kanallara bölerek maksimum indirme hızına ulaşır (Varsayılan 4 kanal, ayarlardan 1, 2, 4, 8, 16 kanal seçilebilir).

### 6. 📁 Otomatik Dizinleme ve Hedef Klasörleme
* İndirilen dosyalar, programın bulunduğu konumdaki `remofix_zip_download/<ArşivAdı>/...` dizini altına, arşiv içindeki orijinal klasör yolları aynen korunarak hiyerarşik biçimde kaydedilir.

### 7. 📊 İndirme Yöneticisi
* Anlık indirme hızı (MB/s), canlı yüzde çubuğu, indirilen / toplam boyut takibi, iptal edebilme ve biten dosyayı Windows Gezgini'nde seçili olarak açan **"Klasörde Göster"** kısayolu.

### 8. 🎨 Remofix Obsidian Modern Arayüz
* `ModernWpfUI` tabanlı koyu (#09090B) tema, yüksek kontrastlı monokrom butonlar, zümrüt yeşili aksiyonlar ve tek tıkla otomatik seçilen modern adres çubuğu deneyimi.

### 9. 📦 Bağımsız Tek Dosya (Standalone Single Exe)
* Bilgisayarda herhangi bir .NET Runtime kurulumu bulunmasına gerek olmaksızın, çift tıklanarak doğrudan çalışan tek parça `RemofixZip.exe`.

---

## 🛠️ Nasıl Çalışır?

ZIP dosya formatında, dosya ve dizinlerin fihristi (Central Directory) dosyanın **en sonunda** yer alır:

```
[Local File 1] [Local File 2] ... [Local File N] [Central Directory] [EOCD / ZIP64 EOCD]
                                                  ▲                   ▲
                                         (Dosya Fihristi)      (Dizin Konumu)
```

1. **HTTP Probe:** Sunucuya `Range: bytes=0-0` isteği atılarak sunucunun `206 Partial Content` destekleyip desteklemediği ve toplam dosya boyutu doğrulanır.
2. **EOCD Okuma:** HTTP Range ile dosyanın son 64 KB'lık kısmı çekilerek *End of Central Directory* bulunur.
3. **Fihrist Ayrıştırma:** Central Directory başlangıç ofseti belirlenir ve yalnızca fihrist aralığı HTTP Range ile çekilerek klasör ağacı oluşturulur.
4. **Hedef Dosya İndirme:** Kullanıcı bir dosyayı seçtiğinde, yalnızca o dosyanın arşiv içindeki bayt aralığı (`[offset ... offset + compressedSize]`) talep edilir ve yerel diske yazılırken gerekirse anında açılır (*Deflate/Store decompress*).

---

## 🚀 Kurulum ve Kullanım

### Hazır İndirme (Tavsiye Edilen)
1. [Releases](https://github.com/istemihangaziparmaksiz/remofix_zip/releases) sayfasından en son `RemofixZip.exe` dosyasını indirin.
2. Kurulum gerekmez, dosyayı çalıştırın.

### Kullanım Adımları
1. **Doğrudan Link İle:**
   - URL kutusuna incelenecek doğrudan ZIP linkini yapıştırın ve **"İçeriği Tara"** butonuna basın.
   - Açılan klasör ağacından indirmek istediğiniz dosyaları işaretleyin ve **"Seçilenleri İndir"** butonuna tıklayın.
2. **Dahili Tarayıcı İle:**
   - Sol menüden **"Dahili Tarayıcı"** modülüne geçin.
   - Dosyanın bulunduğu web sitesine gidin ve indirme linkine tıklayın.
   - Ekrana gelen diyalogda **"İncele"** butonuna bastığınızda dosya fihristi otomatik olarak önünüze gelecektir.

---

## 🔨 Kaynak Koddan Derleme

Projeyi yerel ortamınızda kaynak koddan derlemek için:

```bash
# 1. Depoyu klonlayın
git clone https://github.com/istemihangaziparmaksiz/remofix_zip.git
cd remofix_zip

# 2. Bağımlılıkları geri yükleyin
dotnet restore

# 3. Bağımsız Tek Dosya (.exe) olarak derleyin
dotnet publish RemofixZip.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish/
```

Derlenen `RemofixZip.exe` dosyası `publish/` klasörü içinde oluşacaktır.

---

## 💻 Teknolojiler

- **Platform:** C# 12, .NET 8.0 (Windows WPF)
- **UI Framework:** [ModernWpfUI](https://github.com/KDE/ModernWpf) (Windows 11 Fluent & Dark Obsidian Theme)
- **Web Engine:** Microsoft WebView2 (Chromium Core)
- **Arşiv & Sıkıştırma Motoru:** [SharpCompress](https://github.com/adamhathcock/sharpcompress) (ZIP, ZIP64, AES, Deflate)
- **Ağ:** Asenkron `HttpClient` + HTTP Range Stream Provider

---

## 📄 Lisans

Bu proje **MIT Lisansı** ile lisanslanmıştır. Detaylar için [LICENSE](LICENSE) dosyasına göz atabilirsiniz.

---

<div align="center">
  <sub>Remofix Suite tarafından geliştirilmiştir.</sub>
</div>
