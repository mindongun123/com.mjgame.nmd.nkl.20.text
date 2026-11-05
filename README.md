# MJGame Text — `com.mjgame.nmd.nkl.20`

Bộ công cụ chữ cho UI game mobile, xây trên **TextMeshPro**: hiệu ứng, màu/gradient, định dạng số và thời gian không tạo GC, typewriter, số chạy.
Package không thay thế TMP mà chạy bên trên nó, nên vẫn giữ được batching, font fallback, đa ngôn ngữ và tooling của Unity. Mọi thứ đều là component hoặc extension, gắn vào `TextMeshProUGUI` (UI) hoặc `TextMeshPro` (text 3D trên object) có sẵn là dùng được.

- Unity 2022.3+, TextMeshPro 3.0.7
- Namespace: `com.mjgame.nmd.nkl`
- Menu: **Add Component › com.mjgame.nmd.nkl › Text**, **GameObject › com.mjgame.nmd.nkl › Text**
- Settings: **Tools › com.mjgame.nmd.nkl › Text › Settings...**
- Thư viện effect (xem trước mọi tag, copy chuỗi mẫu): **Tools › com.mjgame.nmd.nkl › Text › Effect Gallery...**
- Lịch sử thay đổi: [CHANGELOG.md](CHANGELOG.md)

---

## Mục lục
1. [Chọn công cụ theo tình huống](#chọn-công-cụ-theo-tình-huống)
2. [Bắt đầu nhanh](#bắt-đầu-nhanh)
3. [Định dạng số và thời gian (zero-GC)](#định-dạng-số-và-thời-gian-zero-gc)
4. [Font, màu, preset: dùng gì của TMP](#font-màu-preset-dùng-gì-của-tmp)
5. [Effect](#effect) · [Settings: text UI và text 3D](#settings-text-ui-và-text-3d) · [Màu sắc](#màu-sắc)
6. [Tag: effect cho một đoạn chữ](#tag-effect-cho-một-đoạn-chữ)
7. [Typewriter](#typewriter)
8. [TextCounter](#textcounter)
9. [Chữ khối 3D](#chữ-khối-3d)
10. [Danh sách, shop, pool](#danh-sách-shop-pool)
11. [Hiệu năng](#hiệu-năng)
12. [Viết effect mới](#viết-effect-mới)
13. [API](#api)
14. [Test](#test)
15. [Giới hạn đã biết](#giới-hạn-đã-biết) · [Layout: chữ vòng cung, chữ uốn cong](#layout-chữ-vòng-cung-chữ-uốn-cong)
16. [Sample](#sample)

---

## Chọn công cụ theo tình huống

| Bạn cần | Dùng | Chi phí runtime |
|---|---|---|
| Mọi text đồng bộ font, viền, bóng | Material preset của TMP + prefab cho từng loại text | 0 |
| Gradient nhiều màu, theo hướng, dùng chung | `GradientEffect` + `TextGradient` (tĩnh) | Chỉ khi text thay đổi |
| Hiện giá, coin, điểm, % hay thời gian | `text.SetNumber(...)`, `SetAbbreviated`, `SetTime`... | Như `SetText`, không GC |
| Số chạy khi thay đổi (coin, phần thưởng) | `TextCounter` | Chỉ trong lúc đếm |
| Title, popup xuất hiện | `BounceInEffect`, `FadeInEffect` | Chỉ trong lúc chạy (dưới 1 s) |
| Hội thoại, tutorial | `TypewriterEffect` + tag | Chỉ trong lúc gõ |
| Nhấn mạnh liên tục (HOT, SALE, phần thưởng lớn) | `Wave`, `Pulse`, `Rainbow`... + sub-Canvas | Mỗi frame; dùng cho ít text |
| Title kiểu cartoon dày | `TextExtrude` | +1 mesh, +1 drawcall |
| Chữ trên object (tên NPC, số damage) | `TextMeshPro` + `TextFX`, xem [Settings](#settings-text-ui-và-text-3d) | Như text UI |

---

## Bắt đầu nhanh

```csharp
using com.mjgame.nmd.nkl;

price.SetAbbreviated(15300, prefix: "<sprite name=coin> ");   // "🪙 15.3k"
timer.SetTime(secondsLeft, TimeFormat.Auto, roundUp: true);   // "1:02:05"
title.GetComponent<BounceInEffect>().Play();                  // khi mở popup
coins.GetComponent<TextCounter>().Add(250);                   // số chạy lên
```

Trong Editor, cách nhanh nhất: gắn **Add Component › com.mjgame.nmd.nkl › Text › Text FX** cho text, rồi viết tag có sẵn trong chuỗi, ví dụ `Nhận <wave>1000</wave> <gold>vàng</gold>` (xem [Tag dựng sẵn](#tag-dựng-sẵn)). Không cần thêm effect nào.

Muốn effect áp cả text hoặc chỉnh riêng một item: **Add Component › com.mjgame.nmd.nkl › Text › Effects › Wave**, rồi bấm **Preview** trên Inspector để xem ngay mà không cần Play.

---

## Định dạng số và thời gian (zero-GC)

Extension method trên `TMP_Text`. Tất cả ghi vào một buffer `char[]` dùng chung và gọi `TMP_Text.SetText(char[], int, int)`, nên **không tạo string và không tạo GC**.

| Method | Ví dụ |
|---|---|
| `SetNumber(1234567)` | `1,234,567` |
| `SetNumber(1234567, separator: '.')` | `1.234.567` |
| `SetAbbreviated(15300)` | `15.3k` |
| `SetAbbreviated(1250000, decimals: 2)` | `1.25M` |
| `SetAbbreviated(9999, from: 10000)` | `9,999` (chỉ rút gọn từ 10k) |
| `SetDecimal(3.14159, 2)` | `3.14` |
| `SetPercent(0.756)` / `SetPercent(0.756, 1)` | `76%` / `75.6%` |
| `SetFraction(3, 10, "Lv ")` | `Lv 3/10` |
| `SetTime(75)` | `01:15` |
| `SetTime(3725)` | `1:02:05` |
| `SetTime(97200, TimeFormat.Compact)` | `1d 3h` |
| `SetTime(0.2, roundUp: true)` | `00:01` (đếm ngược không hiện 0 quá sớm) |

Mọi method đều nhận `prefix` / `suffix`, có thể chứa rich text: `SetNumber(500, "<sprite name=gem> ", " gems")`.

**Đơn vị rút gọn**: k, M, B, T, Qa, Qi, Sx, Sp, Oc, No, Dc, Ud, Dd, Td, Qad, Qid, Sxd, Spd, Ocd, Nod, Vg (mỗi bậc ×1000, tới 10⁶³). Bản `long` chính xác tuyệt đối nhưng chỉ tới `9.2Qi`; game idle dùng bản `double`: `SetAbbreviated(1.5e21)` → `1.5Sx`. Phần thập phân bị **cắt bớt** (1,999 → `1.9k`, không bao giờ hiện lớn hơn giá trị thật), bỏ số 0 ở cuối, và phần nguyên có 3 chữ số thì không hiện thập phân (`150k`).

**Đa ngôn ngữ cho thời gian**:
```csharp
static readonly TimeLabels Vi = new TimeLabels(" ngày", " giờ", " phút", " giây");
text.SetTime(97200, TimeFormat.Compact, labels: Vi);   // "1 ngày 3 giờ"
```

**Dùng buffer của bạn** (ví dụ ghép nhiều giá trị trong một text): `TextFormat.WriteNumber(buffer, index, value)`. Mỗi hàm `Write*` trả về index tiếp theo để ghép tiếp.

> Chỉ gọi khi giá trị **thay đổi**. TMP dựng lại mesh mỗi lần set text, kể cả khi nội dung giống hệt. Ví dụ với đồng hồ: chỉ set khi số giây đổi (xem `ShopSample.cs`).

---

## Font, màu, preset: dùng gì của TMP

Package **không bọc lại** những thứ TMP đã có sẵn. Font, cỡ chữ, màu, căn lề, spacing đều chỉnh trên component TextMeshPro như bình thường. Muốn đồng bộ cả game:

| Cần | Dùng (có sẵn trong TMP / Unity) |
|---|---|
| Viền, bóng, glow, bevel dùng chung | **Material preset** của font: sửa một preset thì mọi text dùng nó đổi theo |
| Một loại text (Title, Price, Button...) giống nhau ở mọi nơi | **Prefab / Prefab Variant** của text đó |
| Định dạng một đoạn chữ bằng tag | **TMP Style Sheet** (`<style="Title">`) |
| Gradient 2–4 màu cố định | `Color Gradient` trên component TMP |

Package chỉ thêm những **cấu hình riêng** mà TMP không có:
- **`TextGradient`**: preset gradient nhiều màu dùng chung (xem [Màu sắc](#màu-sắc)).
- Tham số của từng effect, nằm ngay trên component effect.

---

## Effect

| Component | Mô tả | Kiểu chạy | Tham số chính |
|---|---|---|---|
| `WaveEffect` | Chữ lượn sóng | Lặp | `amplitude`, `speed`, `spacing` |
| `ShakeEffect` | Chữ rung | Lặp, hoặc dừng sau `duration` | `amplitude`, `rate`, `duration`, `falloff` (biên độ giảm dần ở đoạn cuối) |
| `PulseEffect` | Chữ phóng to/thu nhỏ theo nhịp | Lặp | `amount`, `speed`, `spacing` |
| `WobbleEffect` | Chữ lắc quanh tâm | Lặp | `angle`, `speed`, `spacing` |
| `RainbowEffect` | Chữ đổi màu theo vòng màu | Lặp (tĩnh khi `speed = 0`) | `speed`, `spacing`, `saturation`, `mix` |
| `GradientEffect` | Gradient nhiều màu, nhiều hướng | Tĩnh, hoặc lặp khi cuộn | xem [Màu sắc](#màu-sắc) |
| `ColorFlashEffect` | Nhấp nháy về một màu | Một lần, hoặc lặp khi `duration = 0` | `color`, `strength`, `rate`, `duration`, `charDelay`, `skipSpaces` |
| `BounceInEffect` | Chữ nảy vào lần lượt | Một lần | `charDelay`, `charDuration`, `overshoot`, `rise`, `skipSpaces` |
| `FadeInEffect` | Chữ hiện mờ dần và trượt vào | Một lần | `charDelay`, `charDuration`, `slide`, `skipSpaces` |
| `ShineEffect` | Vệt sáng quét ngang chữ (kiểu nút "Claim"). Chỉ đổi màu vertex: không tạo material, vẫn batch | Lặp có nghỉ, hoặc một lần khi tắt `loop` | `color`, `intensity`, `width`, `duration`, `interval`, `slant` |
| `TypewriterEffect` | Gõ từng chữ | Một lần | xem [Typewriter](#typewriter) |

Trường chung:
- `Tag`: để trống thì áp cho cả text; có giá trị thì chỉ áp cho phần nằm trong `<tag>...</tag>`. Dòng gợi ý ngay dưới ô cho biết effect đang áp ở đâu. Effect thêm qua Inspector có sẵn tag theo tên (Wave → `wave`, Color Flash → `flash`, Bounce In → `bounce`...); riêng Typewriter mặc định áp cả text. Effect thêm bằng code (`AddComponent`) có Tag trống, tức là áp cả text.
- `Play On Enable`: tự chạy khi object được bật (chỉ trong Play mode).

Có thể gắn nhiều effect trên cùng một text, chúng chồng lên nhau (ví dụ Bounce In + Wave). Effect một lần tự dừng khi chạy xong; gọi `Play()` để chạy lại.

### Settings: text UI và text 3D
Mở **Tools › com.mjgame.nmd.nkl › Text › Settings...** Cửa sổ chia thành các mục:

| Mục | Setting | Mặc định | Ý nghĩa |
|---|---|---|---|
| Editor | Auto Preview | Bật | Effect tự chạy trong Edit mode. Lưu theo máy của từng người |
| Pause | UI texts | Tắt | Text UI (`TextMeshProUGUI`) có dừng khi `Time.timeScale = 0` không. Tắt = vẫn chạy trong menu pause |
| Pause | World texts (3D) | Bật | Text 3D (`TextMeshPro` trên object) dừng khi pause game |
| Performance | Update rate | Every frame | Số lần vẽ lại effect lặp (wave, rainbow, shine…) mỗi giây khi Play: Every frame, 60, 30, 20 hoặc 15. Game 60 fps đặt 30 thì effect vẫn mượt mà tốn khoảng một nửa CPU; tốc độ animation không đổi. Game đã chạy 30 fps thì để Every frame |
| Distance scale | UI texts × / World texts × | 1 / 0.1 | Hệ số nhân cho khoảng cách: biên độ Wave/Shake, `rise` của Bounce, `slide` của Fade |
| Motion | Strength | 1 | Độ mạnh của chuyển động: biên độ Wave/Shake, góc Wobble, mức phóng Pulse, độ nảy của Bounce, khoảng trượt của Fade. 0 = không chuyển động (màu và độ mờ vẫn chạy) |
| Per quality level | Add quality level | trống | Ghi đè Motion strength và Update rate cho từng mức trong **Project Settings › Quality**, ví dụ mức Low: motion 0.5, 30 lần/giây |

**Giảm chuyển động cho người chơi**: gắn với tuỳ chọn trong menu game, ví dụ `TextFXSettings.SetPlayerMotionScale(reduceMotion ? 0f : -1f)`. Giá trị này thắng cả asset lẫn mức chất lượng; truyền số âm để bỏ. Package không tự lưu, game tự lưu lựa chọn của người chơi (PlayerPrefs...) và gọi lại khi khởi động.

Nút **Reset to Defaults** ở cuối cửa sổ đưa mọi setting về giá trị mặc định trong bảng trên.

TMP dựng text 3D nhỏ hơn text UI 10 lần ở cùng font size, nên hệ số ×0.1 giúp cùng thông số trông giống nhau trên cả hai loại. Asset có sẵn trong package ở `Runtime/Resources/TextFXSettings.asset` và có tác dụng cả trong build. Cửa sổ Settings sửa thẳng asset này. Nếu asset bị xóa, package dùng giá trị mặc định, và nút **Create Settings Asset** sẽ tạo lại ở chỗ cũ. Khi package cài từ registry hoặc git (chỉ đọc), nút sẽ tạo ở `Assets/Resources/`. Từ code có thể dùng `TextFXSettings.Set(asset)`.

---

## Layout: chữ vòng cung, chữ uốn cong

Đặt chữ theo một đường cong **cố định**: chỉ tính lại khi text đổi, mỗi frame không tốn gì. Thêm component vào text có `TextFX` (Inspector của `TextFX`, nhóm **Layout**: Arc, Curve, Line), xem ngay trong Edit mode.

| Component | Dùng cho | Thông số |
|---|---|---|
| `ArcLayout` | Huy hiệu, banner, logo, chữ quanh vòng quay | `Direction`: Up (∩) hoặc Down (∪). `Size By`: **Angle**, tức là chữ luôn trải đúng `Angle` độ dù dài hay ngắn (360 = vòng tròn); hoặc **Radius**, tức là vòng tròn cố định bán kính `Radius`: chữ dài thì quấn xa hơn, bán kính càng lớn càng phẳng, nhiều text dùng chung một bán kính sẽ nằm trên cùng một đường cong |
| `LineLayout` | Chữ thẳng hoặc nghiêng, thường dùng cùng Size Curve | `Tilt`: góc nghiêng (độ), dương = đi lên về bên phải |
| `CurveLayout` | Chữ kiểu WordArt | `Curve`: hình dạng từ trái (0) sang phải (1), vẽ trong Inspector. `Height`: độ nâng ứng với giá trị 1 (âm = lật ngược) |

Thông số chung: `Anchor` (đầu, giữa hoặc cuối chữ nằm ở điểm neo của đường, ví dụ đỉnh vòng cung), `Rotate Characters` (tắt thì chữ luôn đứng thẳng).

**Size Curve** (mọi layout): bật lên để mỗi chữ có kích thước theo vị trí trong câu. Trục x của curve đi từ 0 (chữ đầu) đến 1 (chữ cuối), trục y là tỉ lệ (1 = bình thường). Ví dụ to dần vào giữa (0 → 0.8, 0.5 → 1.6, 1 → 0.8), hoặc to dần sang phải. **Align** chọn phần nào của chữ thẳng hàng khi phóng: **Bottom** (mặc định) thì chân chữ cùng một đường và chữ to lên phía trên; **Middle** thì tâm các chữ cùng một đường và chữ to ra cả trên lẫn dưới; **Top** thì đỉnh cùng một đường và chữ to xuống dưới. **Keep Spacing** (mặc định bật) giãn khoảng cách theo kích thước mới để chữ to không đè lên nhau. Kết hợp được với Arc, ví dụ "MEGA WIN" vừa cong vừa phình giữa.

- **Kết hợp được với mọi effect**: layout luôn chạy trước (`Order = -100`), nên `<wave>` lượn theo chữ đã uốn, `<gold>` tô màu theo hình dạng mới, chữ khối 3D uốn theo.
- **Nhiều dòng**: mỗi dòng nằm trên một vòng tròn đồng tâm riêng, nên dòng phía trong không bị dồn chữ.
- **Đổi thông số bằng code**: dùng property (`arc.Angle = 120f`) là tự cập nhật. Sửa field của layout tự viết thì gọi `Refresh()`.
- **Layout tự viết**: kế thừa `TextLayoutEffect` và viết `Evaluate(distance, out point, out direction)`, tức là điểm và hướng của đường tại một khoảng cách tính từ điểm neo.

---

## Tag: effect cho một đoạn chữ

### Tag dựng sẵn
Text chỉ cần có `TextFX`. Các tag sau **chạy ngay mà không cần thêm effect**:

| Tag | Effect |
|---|---|
| `<wave>` `<shake>` `<pulse>` `<wobble>` | chuyển động lặp |
| `<bounce>` `<fade>` | xuất hiện một lần |
| `<rainbow>` `<flash>` `<shine>` | màu, vệt sáng |
| `<gold>` `<fire>` `<ice>` `<toxic>` | gradient (từ prefab preset) |

```
<gold>HUYỀN THOẠI</gold> giảm <shake=3>50%</shake> · <wave a=12>Nhận ngay</wave>
```

**Thông số mặc định của cả game** nằm trong prefab `Runtime/Resources/TextFXPresets`. Mở prefab, chỉnh effect có Tag tương ứng (ví dụ Wave `wave` đổi `amplitude`), rồi lưu. Thêm tag mới cũng làm ở đây: add một effect và đặt Tag mới, ví dụ GradientEffect với Tag `vip` thì dùng được `<vip>`. Có thể thay prefab bằng `TextFXPresets.SetPresets(prefab)`, hoặc đăng ký từ code bằng `TextFXPresets.Register("tag", typeof(MyEffect))`.

**Chỉnh riêng một item**: add effect vào text đó, giữ Tag trùng tên (`wave`) rồi sửa thông số. Effect gắn tay **thắng** tag dựng sẵn, chỉ trên text đó. Muốn chỉnh một đoạn nhỏ thì dùng [tham số trong tag](#tham-số-trong-tag): `<wave a=3>`.

Cơ chế: lần đầu text gặp tag dựng sẵn, `TextFX` thêm một bản copy của effect, **ẩn và không lưu vào scene**, rồi dùng lại cho các lần đổi text sau. Text không có tag thì không tạo gì. Effect lặp mà text hiện tại không còn tag của nó thì rời vòng update, nên không tốn gì mỗi frame.

> Effect một lần (`<bounce>`, `<fade>`) chạy khi được tạo và mỗi lần object bật lại. Muốn chạy lại theo ý thì add component và gọi `Play()`.

### Tag của effect tự gắn
Đặt `Tag` của effect, rồi dùng tag đó trong text:
```
Nhận <wave><color=#FFD700>100</color></wave> <shake>vàng!</shake>
```
- Tag không phân biệt hoa thường. Tag không khớp với effect nào thì được giữ nguyên để TMP tự xử lý.
- **Gõ sai tag**: TMP sẽ in nguyên `<wavee>` ra màn hình. Inspector của `TextFX` cảnh báo tag lạ và gợi ý tên gần đúng ("did you mean `<wave>`?"); tag của typewriter (`<p>`, `<speed>`...) bị báo nếu text không có `TypewriterEffect`.
- **Lồng được**: `<wave>Nhận <shake>100</shake> vàng</wave>`, và dùng chung được với `<link>`, `<color>`, `<sprite>` của TMP.
- Tag vẫn hoạt động với chuỗi đã dịch, vì nó nằm ngay trong chuỗi localize.
- Về cơ chế: `TextFX` là preprocessor của text. Nó gỡ tag của package ra khỏi chuỗi và ghi lại vị trí (`TextFX.Spans`), không dùng `<link>` của TMP.

### Tham số trong tag
Mỗi đoạn có thể có tham số riêng, ghi đè giá trị trên component:
```
<wave=12>to</wave> <wave a=3 s=2>nhỏ, chậm</wave> <shake a=5 r=30>rung mạnh</shake>
```
| Effect | Giá trị chính (`=x`) | Tham số khác |
|---|---|---|
| Wave | `a` biên độ | `s` tốc độ, `d` độ lệch giữa các chữ |
| Shake | `a` biên độ | `r` số lần/giây |
| Pulse | `a` mức phóng to | `s`, `d` |
| Wobble | `a` góc | `s`, `d` |
| Rainbow | `s` tốc độ | `d`, `mix` |
| Flash | `c` màu (`red`, `#ff0000`) | `r` số lần/giây, `d` độ trễ giữa các chữ, `a` độ mạnh |
| Bounce | `h` độ cao nảy | `d` độ trễ, `t` thời gian mỗi chữ, `o` độ vọt |
| Fade | `d` độ trễ | `t` thời gian mỗi chữ, `x`, `y` khoảng trượt |
| Shine | `c` màu vệt sáng | `w` độ rộng (theo bề ngang đoạn chữ), `t` thời gian một lần quét, `i` thời gian nghỉ, `a` độ sáng |

Gradient không nhận tham số trong tag (khung bao và chế độ tĩnh tính trên cả đoạn): muốn màu khác thì dùng tag preset như `<gold>`, `<fire>`, hoặc thêm preset mới trong `TextFXPresets`.

Giá trị có thể đặt trong ngoặc: `<wave a="10">`. Số dùng dấu chấm thập phân (`0.5`).

### `<nofx>`: loại trừ khỏi effect
```
<nofx>chữ này đứng yên, giữ màu gốc</nofx>
<nofx=gradient>chỉ bỏ gradient</nofx>
<nofx=gradient,wave>bỏ gradient và wave</nofx>
```
Tên dùng trong danh sách là tên class bỏ chữ "Effect", viết thường (`gradient`, `rainbow`, `wave`, `shake`, `typewriter`...), hoặc `Tag` của effect. `<nofx>` dùng được cả khi effect áp cho toàn bộ text, và lồng được bên trong tag effect.

### Icon (sprite TMP)
Icon được chèn bằng tag của TMP: `<sprite=0>`, `<sprite name="coin">`.
- Effect **chuyển động** (Wave, Bounce, Typewriter...) di chuyển icon cùng chữ.
- Icon được xử lý như một chữ: effect chuyển động di chuyển nó, effect màu tô màu nó.
- Muốn icon **giữ màu gốc** dưới gradient hay rainbow: bọc trong `<nofx>`, ví dụ `RAIN<nofx><sprite=0></nofx>BOW`.
- Muốn icon đứng yên giữa đoạn chữ đang chuyển động: `<wave>Nhận <nofx><sprite name="coin"></nofx> 100</wave>`.
- Kết hợp với định dạng số: `price.SetAbbreviated(15300, prefix: "<sprite name=coin> ")`.

---

## Màu sắc

Có 4 lớp tô màu, từ rẻ nhất đến linh hoạt nhất. Có thể kết hợp các lớp với nhau.

| Lớp | Dùng khi | Chi phí |
|---|---|---|
| `color` / 4 góc `colorGradient` trên component TMP | Màu cố định, gradient 2–4 màu | 0 |
| Material preset (outline, underlay, glow, bevel, **Face Texture** cuộn được) | Viền, bóng, ánh sáng, tô bằng texture | 0 CPU (GPU shader) |
| **`GradientEffect`** tĩnh | Gradient nhiều màu, theo hướng, theo từng chữ, dùng preset | Chỉ áp khi text thay đổi |
| `GradientEffect` cuộn / `RainbowEffect` / `ColorFlashEffect` | Màu chạy, nhấp nháy | Mỗi frame khi đang chạy |

### GradientEffect
| Trường | Tùy chọn |
|---|---|
| `preset` / `gradient` | `Gradient` của Unity (tối đa 8 mốc màu + alpha). `preset` là asset `TextGradient` dùng chung (**Create › com.mjgame.nmd.nkl › Text › Text Gradient**), sửa một chỗ thì mọi text dùng nó đổi theo |
| `direction` | `Horizontal`, `Vertical` (trên xuống dưới), `Angle` (theo `angle`), `Radial` (từ tâm ra), `Characters` (theo thứ tự chữ, không phụ thuộc layout) |
| `coloring` | `Smooth` (mỗi đỉnh một màu, chuyển màu mượt cả trong một chữ) / `PerCharacter` (mỗi chữ một khối màu) |
| `repeat`, `offset`, `wrap` | Gradient lặp bao nhiêu lần trên toàn text, dịch vị trí, kiểu lặp `Repeat` / `PingPong` / `Clamp` |
| `scrollSpeed` | Số vòng gradient mỗi giây, có thể âm. **0 = tĩnh: không tốn chi phí mỗi frame và hiện luôn trong Edit mode** |
| `blend`, `amount` | `Replace` / `Multiply` (giữ màu sẵn có, chỉ phủ thêm sắc) / `Add` (tạo vệt sáng), độ mạnh 0..1 |
| `useGradientAlpha` | Nhân alpha của chữ với alpha của gradient (chữ mờ dần ở một đầu) |
| `Tag` | Chỉ tô một đoạn, ví dụ `Nhận <grad>1000 vàng</grad>` |

Gradient được tính theo **khung bao của đoạn chữ bị ảnh hưởng**. Vì vậy với `repeat = 1`, gradient trải đúng một lần từ đầu đến cuối đoạn đó, dù chữ dài hay ngắn và cỡ chữ bao nhiêu.

```csharp
var g = text.GetComponent<GradientEffect>();
g.preset = goldGradient;
g.direction = GradientEffect.Direction.Angle;
g.angle = 30f;
g.scrollSpeed = 0.35f;   // vệt sáng chạy qua
```

### ColorFlashEffect
Nhấp nháy về một màu, ví dụ đỏ khi không đủ tiền, trắng khi nhận thưởng: `color`, `strength`, `rate` (số lần/giây), `duration` (0 = lặp mãi), `charDelay` (vệt màu quét qua từng chữ).

### RainbowEffect
Vòng màu HSV theo từng chữ, chỉ chỉnh `saturation`, `brightness`, `mix`. Muốn chọn màu riêng, chọn hướng hay tô mượt thì dùng `GradientEffect` với preset Rainbow.

### So với SuperTextMesh
| | STM | Package này |
|---|---|---|
| Gradient nhiều mốc + preset asset | ✓ | ✓ (`TextGradient`) |
| Ngang / dọc | ✓ | ✓, thêm `Angle`, `Radial`, `Characters` |
| Mượt / theo từng chữ | ✓ | ✓ |
| Cuộn | ✓ | ✓ |
| Kiểu lặp (Repeat / PingPong / Clamp) | – | ✓ |
| Blend (Replace / Multiply / Add + amount) | Chỉ nhân với color | ✓ |
| Alpha từ gradient | – | ✓ |
| Gradient tĩnh không tốn gì mỗi frame | Tính lại mỗi lần dựng mesh | Áp một lần, chỉ áp lại khi text thay đổi |
| Tô bằng texture | ✓ (shader riêng) | Dùng Face Texture có sẵn của material TMP |

---

## Typewriter

```csharp
var typer = GetComponent<TypewriterEffect>();
typer.onCharacterRevealed.AddListener(i => audio.PlayOneShot(tick));
typer.onComplete.AddListener(ShowNextButton);

typer.Show("Xin chào! Hôm nay bạn muốn làm gì?");
if (!typer.IsComplete) typer.Skip();       // khi người chơi chạm màn hình
```

| Trường | Ý nghĩa |
|---|---|
| `charactersPerSecond` | Tốc độ gõ |
| `fadeDuration` | Thời gian hiện mờ của mỗi chữ (0 = hiện ngay) |
| `punctuationPause` / `punctuation` | Thời gian nghỉ thêm sau dấu câu |
| `restartOnTextChange` | Đổi text khi đang gõ thì gõ lại từ đầu |
| `onCharacterRevealed(int)` / `onComplete` | Event |
| `onEvent(string)` | Gọi khi gõ tới tag `<ev=...>` |

### Tag điều khiển trong câu thoại
```
Khoan đã...<p=0.8> Cái gì <speed=x0.3>đang tới</speed>?<ev=shake> CHẠY ĐI!
```
| Tag | Tác dụng |
|---|---|
| `<p=0.5>` / `<pause=0.5>` | Dừng 0.5 s trước chữ tiếp theo |
| `<speed=10>…</speed>` | 10 chữ/giây cho đoạn này |
| `<speed=x2>…</speed>` | Gấp đôi tốc độ gốc (`x0.5` = chậm một nửa) |
| `<ev=shake>` / `<event=shake>` | Gọi `onEvent("shake")` khi gõ tới, ví dụ để rung camera hay đổi biểu cảm |

Các tag này bị gỡ khỏi chữ hiển thị. `Skip()` bỏ qua các lần dừng nhưng vẫn gọi các `<ev>` còn lại.

Chữ chưa gõ được ẩn bằng alpha của vertex, nên layout không nhảy và text không phải dựng lại trong lúc gõ.

---

### Âm thanh khi gõ: `TypewriterSound`
Gắn **Add Component › com.mjgame.nmd.nkl › Text › Typewriter Sound** cạnh `TypewriterEffect`, rồi kéo một hoặc nhiều clip vào `clips`.

| Field | Mặc định | Ý nghĩa |
|---|---|---|
| `clips` | | Mỗi lần chọn ngẫu nhiên một clip; có từ 2 clip trở lên thì không lặp lại clip vừa phát |
| `pitchRange` | 0.95–1.05 | Pitch ngẫu nhiên cho mỗi tiếng, để không nghe như tiếng súng máy |
| `everyNthCharacter` | 1 | 2 = cứ hai chữ mới kêu một lần |
| `minInterval` | 0.04 s | Khoảng cách tối thiểu giữa hai tiếng khi chữ chạy nhanh |
| `skipSpaces` / `skipPunctuation` | bật / tắt | Không kêu ở khoảng trắng / dấu câu |
| `muteOnSkip` | bật | `Skip()` hiện hết chữ mà không phát tiếng |

`source` để trống thì dùng `AudioSource` trên object (thiếu thì tự thêm). Tắt toàn cục theo setting âm thanh của game: `TypewriterSound.Muted = true`. Không tạo GC mỗi chữ.

### Biến mất: `FadeOutEffect`
```csharp
var fadeOut = GetComponent<FadeOutEffect>();
fadeOut.onComplete.AddListener(() => panel.SetActive(false));
fadeOut.Play();      // chữ biến mất lần lượt
fadeOut.HideNow();   // ẩn ngay, không animation
fadeOut.Stop();      // hiện lại
```
- `order`: `Forward` (từ đầu), `Backward` (từ cuối, như xoá chữ), `AllAtOnce`.
- `slide`, `endScale`: trượt và thu nhỏ khi mờ đi (theo `MotionScale`).
- **Play On Enable mặc định tắt**, để chữ không biến mất ngay khi bật. Không chạy preview trong Edit mode.
- Ẩn xong thì chữ vẫn ẩn nhưng **rời vòng update**, không tốn gì mỗi frame; text đổi nội dung thì vẫn ẩn. Dùng `IsHidden` để kiểm tra.
- Có nút **Fade Out** trong nhóm Appear của inspector `TextFX`.

## TextCounter

```csharp
var coins = GetComponent<TextCounter>();
coins.SetValue(1000);   // nhảy thẳng tới giá trị, không animation
coins.Add(250);         // đếm lên 1250 (gọi được từ UnityEvent với tham số int/float)
coins.CountTo(99999);
coins.SetValue(0.199);  // số lẻ: Value/Target là double
coins.Complete();       // kết thúc ngay
```

| Trường | |
|---|---|
| `display` | `Number` (`1058.6`), `Abbreviated` (`1.9k`), `Time` (giá trị là giây: `02:05`) |
| `prefix` / `suffix` | Ví dụ `"$ "` |
| `decimals` | Số chữ số lẻ tối đa, dùng chung cho `Number` và `Abbreviated`; cắt xuống, bỏ số 0 ở cuối (`0.199`, `2.5`, `199`) |
| `separator` | Dấu thập phân (mặc định `.`, để trống cũng là `.`). Không phân cách hàng nghìn. Đặt `,` để ra `1058,6`, `1,9k` |
| `abbreviateFrom` | Cho chế độ `Abbreviated` |
| `timeFormat` | Cho chế độ `Time` |
| `duration` | Thời gian đếm (ease-out, unscaled time) |

Chỉ render lại khi số hiển thị thay đổi; không tạo GC.

---

## Chữ khối 3D

1. Thêm **Text Extrude** vào text.
2. Nhấn chuột phải vào component › **Create Extrude Layer**.

Lớp khối nằm phía sau, tự đồng bộ chữ, font, cỡ, căn lề và vị trí. Nếu text có `TextFX`, lớp khối cũng chạy theo effect.

| Trường | |
|---|---|
| `offset` | Độ lệch của lớp khối, đơn vị UI (mặc định `(0, -9)`). Text 3D nhân thêm World distance scale của `TextFXSettings` |
| `color` | Màu lớp khối |
| `worldDepth` | Chỉ text 3D: lớp khối lùi ra sau theo Z, `sortingOrder` nhỏ hơn chữ chính 1 |

Không sửa trực tiếp object `_Extrude`: mọi thay đổi bị ghi đè ở lần đồng bộ sau.

---

## Danh sách, shop, pool

Package được thiết kế để dùng cho danh sách hàng trăm item:

- **Text không chạy effect thì tốn 0.** `TextFX` không có effect đang chạy sẽ ra khỏi vòng update và ngừng nghe event của TMP. 200 item đứng yên không tốn gì mỗi frame.
- **Effect lúc mở** (Bounce In, Fade In) cho mọi item: chạy khoảng 0.5 s rồi tự ngủ.
- **Effect lặp** chỉ dùng cho vài item nổi bật (HOT, item đang chọn), và đặt mỗi cái trong một **sub-Canvas** riêng.
- **Item được tái sử dụng** (ScrollView ảo hóa, pool) khi đổi nội dung:
  ```csharp
  fx.ResetAll();                 // dừng effect, trả mesh về gốc ngay
  nameText.SetText(data.name);
  priceText.SetAbbreviated(data.price);
  fx.PlayAll();                  // nếu muốn chạy lại hiệu ứng xuất hiện
  ```
- Item bị tắt rồi bật lại (`SetActive`) thì không cần làm gì: `Play On Enable` tự chạy lại, còn `OnDisable` tự trả mesh về gốc.

Xem tab **Shop** trong [Sample](#sample) (`ShopSample.cs`, `ShopSampleItem.cs`).

---

## Hiệu năng

### Đo trong sample shop (Unity Editor, 210 text có `TextFX`)
| Trạng thái | Text trong vòng update | `MJGame.TextFX.Tick` |
|---|---|---|
| Đứng yên (chỉ 10 huy hiệu HOT đang lặp) | 10 / 210 | ~0.2–0.3 ms/frame |
| Ngay sau khi bấm Open (100 item nảy vào cùng lúc) | 112 / 210 | chỉ trong ~0.5 s, sau đó về lại 10 |

Số liệu đo trong Editor, nơi có thêm chi phí debug; trên build thật sẽ nhẹ hơn. Nên đo lại trên thiết bị đích bằng Profiler (marker `MJGame.TextFX.Tick`).

### Vì sao nhẹ
| | |
|---|---|
| **Không dựng lại text** | Effect chỉ sửa vertex rồi gọi `UpdateVertexData`, không gọi `ForceMeshUpdate` mỗi frame. |
| **Ngủ khi rảnh** | Không có effect chạy thì không ở trong vòng update và không nghe event. |
| **Không tạo material** | Chỉ dùng `fontSharedMaterial`, nên không thêm drawcall và vẫn batch được. |
| **Không GC** | Effect, counter và formatter đều không cấp phát bộ nhớ mỗi frame. |
| **Copy tối thiểu** | Chỉ copy phần vertex đang dùng. |
| **Một listener cho cả game** | Một `TextChangeDispatcher` duy nhất nghe event đổi text của TMP rồi chuyển tới đúng `TextFX` đang theo dõi text đó. 200 text dùng gradient tĩnh không làm mỗi lần đổi chữ tốn thêm 200 callback. |
| **Bỏ qua text đang khuất** | Khi Play, text chỉ chạy effect lặp (wave, rainbow...) sẽ không được cập nhật nếu Canvas tắt, `CanvasGroup` alpha = 0, bị `RectMask2D` che (trượt khỏi ScrollRect) hoặc là text 3D ngoài camera. Effect chạy một lần (typewriter, fade-in) vẫn chạy để xong đúng giờ. Tắt bằng `TextFX.CullHiddenTexts = false`. `Mask` (stencil) không che theo kiểu này, nên dùng `RectMask2D` cho list dài. |
| **Chỉ upload phần bị đổi** | Mỗi effect khai báo `Modifies` (vị trí, màu hoặc cả hai). Wave/Shake chỉ gửi vị trí, Rainbow/Gradient chỉ gửi màu. |
| **Tham số tag parse một lần** | `<wave a=10>` được đổi sang số/màu khi text dựng lại, mỗi frame chỉ tra mảng. |
| **Prewarm** | Gọi `TextFXPresets.Prewarm()` ở màn loading để nạp sẵn preset và settings, tránh giật lần đầu một tag xuất hiện. |
| **LUT cho gradient** | Gradient được lấy mẫu sẵn 128 điểm; mỗi vertex chỉ tra bảng thay vì gọi `Gradient.Evaluate`. Gán gradient hay preset mới được nhận tự động; sửa key bằng code thì gọi `RefreshGradient()`. |

### Chi phí còn lại
Khi effect đang chạy, việc gửi mesh mới làm **Canvas chứa text đó phải rebatch**. Với effect lặp trong UI lớn, hãy dùng sub-Canvas; Inspector của `TextFX` có cảnh báo và nút **Add sub-Canvas**.

---

## Viết effect mới

```csharp
using com.mjgame.nmd.nkl;
using TMPro;
using UnityEngine;

[AddComponentMenu("com.mjgame.nmd.nkl/Text/Effects/Swing")]
public sealed class SwingEffect : TextEffect
{
    public float angle = 15f;

    public override bool IsLooping => true;

    protected override bool Apply(TMP_TextInfo info)
    {
        int n = 0;                                   // thứ tự trong vùng effect
        for (int r = 0; r < RangeCount; r++)         // vùng: cả text hoặc các đoạn <tag>
        {
            Vector2Int range = GetRange(r);
            for (int c = range.x; c < range.y; c++, n++)
            {
                if (!info.characterInfo[c].isVisible) continue;
                Rotate(info, c, Mathf.Sin(Time * 4f + n) * angle);
            }
        }
        return true;                                 // false = effect đã xong
    }
}
```
Trước mỗi lần `Apply`, vertex đã được trả về trạng thái gốc.
- Helper: `Offset`, `Scale`, `Rotate`, `MultiplyAlpha`, `TintColor`, `CharacterIndexAt`, `Time`, `AffectedCount`.
- Khoảng cách nhân với `UnitScale` để chạy đúng trên cả text UI và text 3D: `Offset(info, c, Vector3.up * height * UnitScale)`.
- Chuyển động (khoảng cách, góc, mức phóng) nhân thêm `MotionScale` để tôn trọng tuỳ chọn giảm chuyển động.
- Hook: `OnPlay()`, `OnTextRebuilt()`.
- Effect chỉ sửa vị trí hoặc chỉ sửa màu thì override `Modifies` (ví dụ `=> TMP_VertexDataUpdateFlags.Vertices`) để giảm dữ liệu gửi lên mỗi frame. Không override thì mặc định là cả hai.

---

## API

**`TextFX`**: `Text`, `IsPlaying`, `IsAwake`, `PlayAll()`, `StopAll()`, `ResetAll()`, `AddFollower()`, `RemoveFollower()`, `Reparse()`, `CullHiddenTexts`

**`TextEffect`**: `Play()`, `Stop()`, `IsPlaying`, `IsLooping`, `IsAnimated`, `Modifies`, `TagName`, `AutoPreview`; cho effect tự viết: `UnitScale`, `GetRangeArgs()`

**`TextFXSettings`**: `Instance`, `Set()`, `IsWorld()`, `UsesTimeScale()`, `UnitScale()`, `MotionScale`, `MaxUpdatesPerSecond`, `SetPlayerMotionScale()`

**`ArcLayout`** / **`CurveLayout`**: `Angle` (âm = cong xuống), `Radius`, `Direction`, `SizeBy` / `Curve`, `Height`; chung: `TextAnchor`, `RotateCharacters`, `UseSizeCurve`, `SizeCurve`, `SizeAlignment`, `KeepSpacing`, `Refresh()`; `LineLayout`: `Tilt`

**`TextFXPresets`**: `Register()`, `SetPresets()`, `Reload()`, `Prewarm()`

**`TagArgs`**: `Parse()`, `TryGet()`, `GetFloat()`, `GetColor()`

**`TypewriterEffect`**: `Show(string)`, `Skip()`, `IsComplete`, `onCharacterRevealed`, `onComplete`

**`TextCounter`**: `Value`, `Target`, `IsCounting`, `SetValue(double)`, `CountTo(double)`, `Add(int|long|float|double)`, `Complete()`, `onComplete`

**`GradientEffect`**: `ActiveGradient`, `CreateRainbow()`; **`TextGradient`**: `gradient`

**`TMPTextExtensions`**: `SetNumber`, `SetAbbreviated`, `SetDecimal`, `SetPercent`, `SetFraction`, `SetTime`

**`TextFormat`**: `WriteString`, `WriteNumber`, `WriteAbbreviated`, `WriteDecimal`, `WritePercent`, `WriteFraction`, `WriteTime`

---

## Test
**Window › General › Test Runner › EditMode** › `com.mjgame.nmd.nkl.20.Tests`: 84 test, gồm
- định dạng số, rút gọn, thời gian, phần trăm, kể cả các giá trị biên (`long.MinValue`/`MaxValue`, số âm, NaN);
- tag: gỡ khỏi chữ, lồng nhau, tham số, `<nofx>` (tất cả / theo tên), tag lạ, hoa thường;
- icon: được tô như chữ, `<nofx>` giữ màu gốc;
- typewriter: `<p>`, `<speed=x..>`, `<ev>`;
- dispatcher chỉ theo dõi text đang thức;
- vòng đời effect: thức/ngủ, khôi phục mesh, `ResetAll`, effect một lần tự kết thúc;
- typewriter: ẩn chữ, `Skip`, event;
- gradient tĩnh / cuộn, từng chữ, blend, color flash;
- Pulse, Wobble, Rainbow, Fade In, Shake có `duration`, `skipSpaces`, tham số tag của Flash/Bounce, đọc màu từ tag;
- text 3D: hệ số khoảng cách ×0.1, chọn timeScale theo loại text;
- counter, extension method.

---

## Giới hạn đã biết
- Tag của package có tên trùng tag của TMP (ví dụ đặt `Tag` effect là `b`) sẽ bị package lấy mất; nên dùng tên riêng.
- Tag point của typewriter (`<p>`, `<speed>`, `<ev>`) chỉ được gỡ khi text có `TypewriterEffect`.
- Follower (lớp extrude) phải cùng nội dung và layout với text chính; `TextExtrude` tự lo việc này.
- Preview trong Edit mode không lưu vào scene. Thoát preview hoặc reload script thì text trở về bình thường.
- `TMPTextExtensions` dùng buffer tĩnh: chỉ gọi trên main thread (giống TMP). Prefix/suffix có tag của package (`<wave>`) trên text có `TextFX` thì mỗi lần gán tạo một string, vì TMP chỉ chạy preprocessor cho string; không có tag thì không tạo GC.
- Text rất dài với effect lặp vẫn tốn CPU theo số ký tự.
- Mọi text không phải `TextMeshProUGUI` đều được coi là text 3D (hệ số và timeScale của World).
- Text đang khuất chỉ được bỏ qua khi nó chỉ chạy effect lặp (xem `CullHiddenTexts`). Effect chạy một lần vẫn chạy cho xong; text nằm dưới `Mask` (stencil) không được coi là khuất. Nên dừng effect khi object trả về pool.
- Layout (`ArcLayout`, `CurveLayout`) chỉ dời và xoay các ký tự, còn khung `RectTransform` vẫn là khung chữ thẳng: vùng bấm, `RectMask2D` và Layout Group vẫn tính theo khung cũ. Gạch chân và gạch ngang của TMP không uốn theo.
- `GradientEffect` không nhận tham số trong tag; dùng tag preset (`<gold>`...) thay thế.

---

## Sample
Sample không nằm sẵn trong project: mở **Window › Package Manager**, chọn **MJGame Text**, tab **Samples**, bấm **Import** ở mục **Demo**. Unity chép sample vào `Assets/Samples/MJGame Text/<version>/Demo/`. Trong repo package, sample nằm ở `Samples~/Demo` (thư mục có `~` bị Unity bỏ qua), nên project cài package không biên dịch script sample cho tới khi import.

| Scene | Nội dung |
|---|---|
| `sample.unity` | Một scene, năm tab ở cạnh dưới: **Effects**, **Layout**, **Shop**, **Extras** và **World** |
| Tab Effects | Mọi effect, tag, typewriter, counter, extrude; nút Replay / Skip / +1234 |
| Tab Layout | `ArcLayout` cong lên, cong xuống, vòng tròn 360°, nhiều dòng, kết hợp `<gold>`/`<wave>`; `CurveLayout` kiểu WordArt; Size Curve: phình giữa, vừa cong vừa phình, to dần trên đường nghiêng |
| Tab Extras | Tag dựng sẵn không cần component (`<pulse>`, `<flash>`, `<bounce>`, `<fade>`, `<shine>`, `<nofx=wave>`), `ShineEffect`; hội thoại có `TypewriterEffect` + `<ev=...>` + `TypewriterSound` (âm thanh tạo bằng code, không cần file) + `FadeOutEffect` (nút Close); mọi hàm định dạng (`SetNumber`, `SetAbbreviated` long/double, `SetDecimal`, `SetPercent`, `SetFraction`, `SetTime` Auto/Compact + `TimeLabels` tiếng Việt); nút Reduce motion (`SetPlayerMotionScale`) và Update rate (sửa một bản copy của settings, không đụng asset) |
| Tab World | Text 3D (`TextMeshPro` trên object): bảng tên `TextExtrude` + Wave + `<gold>`, biển xoay `ArcLayout` 360° + Rainbow, số sát thương dùng pool (`BounceIn` → `FadeOut`, `ResetAll`, crit dùng `<shake>` trong prefix của `SetNumber`), vật bay ra khỏi camera để minh hoạ culling; nút Pause đặt `Time.timeScale = 0`: text 3D dừng, text UI vẫn chạy |
| Tab Shop | Shop 100 item: giá rút gọn, mua hàng (rung khi thiếu tiền, nảy khi mua được), huy hiệu HOT trong sub-Canvas, coin đếm có animation, đồng hồ sale, nút Open |

Tab bị ẩn thì tắt hẳn, nên text của nó rời vòng update. `SampleTabs` gọi `TextFXPresets.Prewarm()` khi scene mở. Shop chỉ tạo 100 item lần đầu mở tab Shop.

`Gradients/` chứa 5 `TextGradient` mẫu (Fire, Ice, Gold, Rainbow, Toxic). Script sample (`SampleTabs`, `ShopSample`, `ExtrasSample`, `WorldSample`) nằm ở `Scripts/` (assembly `com.mjgame.nmd.nkl.20.Sample`).

> Sample chỉ dùng font trong `Runtime/Font` của package và UI mặc định của Unity, không phụ thuộc asset nào khác của project. Cần import TMP Essential Resources như mọi project dùng TextMeshPro.
