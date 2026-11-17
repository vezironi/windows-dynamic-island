# Windows için macOS tarzı "Ada" — Tasarım Dokümanı

Tarih: 2026-11-17
Referans çizim: `Untitled.png` (proje kökü)

## 1. Amaç ve neden

Windows'un sağ alttaki bildirim kutucukları yerine, ekranın **üst ortasından sarkan**
macOS / Dynamic Island tarzı bir "ada" istiyoruz. Ada iki şey gösterir:

1. **Çalan müzik** (kapak, şarkı adı, sanatçı, ⏮ ⏯ ⏭)
2. **Gelen bildirimler** (uygulama ikonu, başlık, metin)

Kullanıcının isteği estetik ve deneyim odaklı: "tamamen macOS tarzı bir sistem". Başarı =
masaüstünde göze hoş gelen, akıcı animasyonlu, işe/oyuna asla engel olmayan bir ada.

## 2. Hedefler / Hedef olmayanlar / Anti-hedefler

**Hedefler**
- Müzik çalarken küçük ada görünür; üzerine gelince büyüyüp kontrolleri gösterir.
- Başka uygulamaların bildirimleri adada açılır, birkaç saniye sonra kapanır.
- Boştayken (müzik yok, bildirim yok) ada **tamamen görünmez**.
- Tam ekran uygulama öndeyken (oyun, F11 video, sunum) ada **hiç açılmaz**.
- Windows ile otomatik başlar; sistem tepsisinde ikon (çıkış + ayarlar).

**Hedef olmayanlar (v1)**
- Bildirime cevap yazma / bildirimdeki butonlar (Yanıtla, Kabul et vb.).
- Birden fazla monitörde ada (sadece ana ekran).
- Takvim, hava durumu, şarj, zamanlayıcı gibi ek "canlı etkinlikler".
- Tema / renk özelleştirme ekranı (ada her zaman siyah, macOS gibi).

**Anti-hedefler (çalışsa bile başarısızlık sayılır)**
- Oyun oynarken adanın bir kez bile belirmesi veya oyunun odağını çalması.
- Adanın tıklamaları "yutması": ada kapalıyken veya görünmez alanlarda, altındaki
  pencereye tıklama geçmeli.
- Kasıntılı / takılan animasyon; yüksek CPU/RAM (hedef: boşta ~%0 CPU, <100 MB RAM).
- Windows bildiriminin hem sağ altta hem adada **çift** görünmesi (bkz. 5.3).

## 3. Görünüm

Çizimdeki şekil: ekranın üst kenarına yapışık, alt köşeleri yuvarlak, **üst köşeleri
dışa doğru içbükey kıvrılarak ekran kenarına akan** siyah bir çentik (MacBook çentiği).
Bu kıvrım her boyutta korunur.

```
1) Boşta          →  hiçbir şey yok

2) Müzik (küçük)  →  ╰─[▣  ♪ Şarkı adı ]─╯          ~ 220 x 34 px

   Müzik (hover)  →  ╰─[▣  Şarkı Adı        ⏮ ⏯ ⏭]─╯  ~ 420 x 110 px
                          Sanatçı   ━━━━○───  1:12 / 3:40

3) Bildirim       →  ╰─[(ikon)  Başlık             ]─╯ ~ 400 x 80 px
                          Metin (en fazla 2 satır…)
```

- Arka plan saf siyah, metin beyaz/gri, Segoe UI Variable.
- Küçük müzik hâlinde sağda küçük bir ses dalgası (equalizer) animasyonu (macOS'taki gibi).
- Ölçüler DPI'ya göre ölçeklenir (yukarıdakiler %100 ölçekte yaklaşık değerler; builder ayarlayabilir).

## 4. Davranış (onaylandı)

| Durum | Ne olur |
|---|---|
| Boşta | Ada görünmez, pencere tıklamaları geçirir. |
| Müzik başlar | Ada üstten "pıt" diye küçük müzik hâlinde açılır. |
| Müzik durur (pause) | ~5 sn sonra ada kaybolur. Pause'dayken hover ile kontrol hâlâ mümkün (5 sn içinde). |
| Mouse adanın üzerine gelir | ~250 ms bekledikten sonra (tarayıcı sekmesine uzanırken yanlışlıkla açılmasın diye) büyük müzik hâline genişler; mouse ayrılınca ~0.5 sn sonra küçülür. Küçük adaya tıklamak beklemeden büyütür. *(builder kararı)* |
| Bildirim gelir | Kısa, hafif bir bildirim sesi çalar (adanın kendi sesi; ayarlardan kapatılabilir). Ada bildirim hâline genişler, ~5 sn kalır, sonra önceki hâline döner (müzik varsa müziğe, yoksa kaybolur). Mouse bildirimin üzerindeyse süre durur. |
| Arka arkaya bildirim | Kuyruğa girer, sırayla gösterilir. |
| Bildirime tıklama | Bildirimi gönderen uygulama açılır/öne gelir, ada kapanır. |
| Tam ekran uygulama önde | Ada anında gizlenir, hiçbir şey göstermez. O sırada gelen bildirimler adada gösterilmez (Windows bildirim merkezinde kalır), tam ekrandan çıkınca geriye dönük gösterilmez. |
| Normal kullanım (tarayıcı vb.) | Ada normal çalışır. |

**Animasyonlar:** Tüm boyut / şekil geçişleri yay (spring) fiziğiyle — hafif esneme / "overshoot"
ile. İçerik geçişlerinde içerik solar (fade) ve hafifçe ölçeklenir. Hedef 60 fps.

## 5. Kısıtlar ve teknik gerçekler

### 5.1 Müzik — GSMTC
`Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager` kullanılır.
Spotify, tarayıcılar (YouTube vb.), Windows Medya Oynatıcı vb. buraya yazar. Şarkı adı,
sanatçı, kapak (thumbnail stream), oynatma durumu, süre/konum ve ⏮ ⏯ ⏭ komutları buradan.
Birden fazla oturum varsa `GetCurrentSession()` (Windows'un seçtiği) kullanılır.

### 5.2 Bildirimler — UserNotificationListener
`Windows.UI.Notifications.Management.UserNotificationListener`:
- **Paket kimliği gerektirir** (`userNotificationListener` capability). Bu yüzden uygulama
  MSIX olarak paketlenir veya "sparse package / packaging with external location" ile kimlik
  alır. Builder hangisinin daha az sürtünmeli olduğuna karar verir. **Uygulama şimdilik sadece
  kullanıcının kendi bilgisayarı için** (kullanıcı kararı): kendinden imzalı sertifika ile yerel
  kurulum yeterli; installer, güncelleme mekanizması, Store yok.
- İlk açılışta `RequestAccessAsync()` ile kullanıcıdan izin istenir.
- `NotificationChanged` olayının Win32/paketli masaüstü uygulamalarında güvenilir
  çalışmadığı raporları var → **olay + yedek olarak ~1 sn'de bir `GetNotificationsAsync`
  ile yoklama (ID farkı)** yapılır. Bu ilk "spike" ile doğrulanmalı.
- Bildirimden alınanlar: `AppInfo.DisplayInfo.DisplayName`, logo (`GetLogo`), toast'ın
  metin elemanları (ilk = başlık, sonraki = gövde).
- Uygulamanın kendi bildirimleri ve sessiz/boş metinli bildirimler yok sayılır.

### 5.3 Windows'un kendi kutucuklarını susturma
Windows'un toast pop-up'ını başka bir uygulamadan engellemenin resmi API'si **yok**.
Çözüm: kullanıcı Windows'ta **"Rahatsız Etme" (Do Not Disturb)** modunu açar → kutucuklar
çıkmaz, bildirimler bildirim merkezine düşer, ada onları okur ve gösterir.
- İlk açılışta kısa bir yönlendirme ekranı: izin ver + "Rahatsız Etme'yi aç" (ayar sayfasını
  açan buton: `ms-settings:notifications`).
- **Doğrulanması gereken:** DND açıkken listener'ın bildirimleri hâlâ aldığı. Spike'ta ilk iş
  bu test edilir.
- **Yedek plan (kullanıcı kararı):** DND'de listener bildirim göremiyorsa, DND kullanılmaz;
  kullanıcı Windows ayarlarında uygulama bazında "bildirim başlıklarını göster" (banner)
  seçeneğini kapatır. Bildirimler merkeze düşer, ada gösterir. Yönlendirme ekranı bu adımı
  anlatır ve `ms-settings:notifications` sayfasını açar. Kullanıcı bunu kendisi yapacak.
- **Ses:** DND / banner kapalıyken Windows bildirim sesi çalmaz. Ada her bildirimde kendi kısa
  sesini çalar (kullanıcı kararı). Ses dosyası builder'ın seçimi, ayarlardan kapatılabilir.
- **Bilinen kabul:** Windows'un öncelik listesindeki uygulamalar (alarm, arama) DND'de yine
  pop-up gösterebilir → bunlar çift görünebilir. v1'de kabul edildi.

### 5.4 Tam ekran tespiti
İki kontrol birlikte:
1. `SHQueryUserNotificationState` → `QUNS_BUSY`, `QUNS_RUNNING_D3D_FULL_SCREEN`,
   `QUNS_PRESENTATION_MODE` ise tam ekran.
2. Ön plandaki pencerenin dikdörtgeni, bulunduğu monitörün tamamını kaplıyorsa ve
   masaüstü/kabuk penceresi (`Progman`, `WorkerW`, `Shell_TrayWnd`) değilse tam ekran.
Kontrol: `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` + ~1 sn'lik yoklama. Gizleme anlık olmalı.

### 5.5 Pencere
- WPF, `.NET` (kurulu SDK: 11.0.100), `WindowStyle=None`, `AllowsTransparency=True`,
  `Topmost=True`, `ShowInTaskbar=False`, `WS_EX_TOOLWINDOW` (Alt+Tab'da görünmez),
  `WS_EX_NOACTIVATE` (asla odak çalmaz).
- Ada görünmezken / adanın dışındaki şeffaf alanda tıklamalar alttaki pencereye geçer
  (`WS_EX_TRANSPARENT` durumla değiştirilerek veya pencere boyutu adaya göre ayarlanarak — builder seçer).
- Ana monitörün üst ortasında, y = 0. Görev çubuğu üstteyse bile ekranın en üstü.
- Çentik şekli: içbükey üst köşeli `Path` geometrisi, boyut değiştikçe yeniden hesaplanır.

### 5.6 Bildirime tıklayınca uygulamayı açma
Toast'ın kendi "launch" argümanı listener'dan çalıştırılamaz. Bunun yerine uygulamanın
`AppUserModelId`'si ile uygulama başlatılır / öne getirilir (`shell:AppsFolder\<AUMID>` veya
`IApplicationActivationManager`). Yani WhatsApp bildirimi WhatsApp'ı açar ama ilgili sohbete
gitmeyebilir — v1 için kabul.

## 6. Yaklaşımlar (değerlendirilen)

| Yaklaşım | Artı | Eksi | Karar |
|---|---|---|---|
| **C# + WPF** | Windows API'lerine doğrudan erişim, kolay şeffaf/topmost pencere, ~50-80 MB RAM | Yay animasyonları elle yazılmalı | **Seçildi** |
| Tauri (Rust + HTML/CSS) | CSS ile animasyon çok kolay | Rust tarafında ayrı WinRT kodu + köprü, şeffaf pencere/tıklama geçirme uğraşı | Elendi |
| Electron | En kolay arayüz | 150-250 MB RAM sürekli; yine yerel modül gerekir | Elendi (oyun sırasında arka planda ağır) |

Neden WPF: Uygulamanın özü bildirim yakalama, medya kontrolü ve tam ekran tespiti. Üçü de
Windows API'si ve C#'tan en temiz erişiliyor. Kullanıcı WPF'i onayladı.

## 7. Kullanıcının karar verdikleri
- Boştayken görünmez; tam ekranda asla açılmaz; normal kullanımda açılabilir.
- macOS tarzı akış (bölüm 4'teki tablo, kullanıcı "aynen böyle" dedi).
- Teknoloji: WPF.
- Küçük müzik hâli: kapak + şarkı adı + ses dalgası; ⏮ ⏯ ⏭ sadece büyük (hover) hâlde.
  (Çizimdeki küçük hâldeki butonlar yerine bu seçildi.)
- DND işe yaramazsa: uygulama bazında banner kapatma (5.3).
- Bildirim gelince ada kendi sesini çalar.
- Sadece kendi PC'si için; dağıtım yok.

## 7b. Builder'ın verdiği kararlar (kullanıcı kontrol etsin)
- Hover'da ~250 ms gecikme; küçük adaya tıklama anında büyütür.
- Öncelikli uygulamaların (alarm/arama) DND'de çift görünmesi v1'de kabul.
- Ses ayarlardan kapatılabilir.

## 8. Builder'a bırakılanlar
- Proje yapısı, MVVM kullanılıp kullanılmayacağı, sınıf ayrımı.
- Yay animasyonunun matematiği (sabitler, sönüm).
- MSIX mi sparse package mı.
- Ölçülerin ince ayarı, fontlar, equalizer animasyonunun detayı.
- Ayarlar menüsünün içeriği (v1'de en azından: Windows ile başlat aç/kapa, Çıkış).
- Kapak resmi olmayan medya için yer tutucu görsel.

## 9. İlk iş: Spike (riskleri erkenden doğrula)
Planın ilk adımı, kod tabanına girmeyecek küçük bir deneme:
1. Paket kimliğiyle listener bildirimleri okuyabiliyor mu? `NotificationChanged` olayı geliyor mu?
2. **DND açıkken** listener bildirimleri hâlâ görüyor mu?
3. GSMTC Spotify + tarayıcıdan bilgi/kapak alıyor mu?
Sonuçlar bu dokümana not edilir; 2. madde başarısızsa 5.3'teki yedek plana geçilir.
