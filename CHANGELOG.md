# Changelog

Mọi thay đổi đáng chú ý của package được ghi ở đây.
Định dạng theo [Keep a Changelog](https://keepachangelog.com/vi/1.1.0/), version theo [Semantic Versioning](https://semver.org/lang/vi/).

## [Unreleased]

### Added
- **Tag dựng sẵn**: `<wave>`, `<shake>`, `<pulse>`, `<wobble>`, `<rainbow>`, `<flash>`, `<bounce>`, `<fade>`, `<gold>`, `<fire>`, `<ice>`, `<toxic>` chạy được trên mọi text có `TextFX`, không cần thêm effect. `TextFX` tự thêm một effect ẩn và không lưu vào scene cho mỗi tag được dùng.
- **Prefab preset** `Runtime/Resources/NKL/TextFXPresets`: thông số mặc định của tag dựng sẵn cho cả game, và nơi thêm tag mới. API `TextFXPresets.Register()`, `SetPresets()`, `Reload()`. Lưu prefab trong Editor thì tự nạp lại.
- Effect gắn tay có cùng Tag với tag dựng sẵn sẽ thắng trên text đó, dùng để chỉnh riêng một item. Thêm `TextEffect.IsAuto` và `DefaultTag`.
- Inspector của `TextFX` có các nút **Add Wave / Add Shake / ... / Add Gradient / Add Typewriter**: thêm effect đã điền sẵn tag (`wave`...), ghi đè tag dựng sẵn trên text đó. Thêm tag dựng sẵn `<gradient>`.
- **Auto Preview** (bật mặc định): effect có Play On Enable tự chạy trong Edit mode, kể cả effect của tag dựng sẵn, nên Scene/Game view hiện luôn animation. Bật/tắt ở **Tools › com.nmd.nkl › Text › Auto Preview**. Typewriter không tự chạy vì event của nó có thể gọi code game; dùng nút Preview. Mọi effect giờ là `[ExecuteAlways]`.
- Inspector: dưới ô Tag có dòng gợi ý effect áp ở đâu (cả text, hoặc `<wave>...</wave>`). Effect thêm qua Inspector tự điền Tag theo tên (`wave`, `flash`, `bounce`...); Typewriter mặc định áp cả text.
- **Tag lồng nhau và có tham số**: `<wave a=10 s=2>`, `<shake=5>`; đọc qua `TextEffect.GetRangeArgs()` / `TagArgs`.
- **`<nofx>`** loại một đoạn khỏi mọi effect; `<nofx=gradient,wave>` chỉ loại các effect được liệt kê (`TextEffect.EffectKey` hoặc `Tag`).
- **Icon**: sprite TMP (`<sprite>`) được xử lý như chữ; bọc trong `<nofx>` để giữ màu gốc. Thêm `TextEffect.IsSprite()` cho effect tự viết.
- **Tag cho typewriter**: `<p=0.5>` / `<pause>`, `<speed=10>` / `<speed=x2>` … `</speed>`, `<ev=name>` / `<event>` cùng event `onEvent(string)`.
- `TextFX.Spans`, `TextFX.MapToCharacters()`, `TextEffect.ClaimsPointTag()` để effect tự viết có tag riêng.
- `GradientEffect.RefreshGradient()`.
- **`GradientEffect`**: dùng `Gradient` của Unity (nhiều mốc màu + alpha). Hướng `Horizontal`, `Vertical`, `Angle`, `Radial`, `Characters`; kiểu tô `Smooth` / `PerCharacter`; `repeat`, `offset`, kiểu lặp `Repeat` / `PingPong` / `Clamp`; `scrollSpeed`; blend `Replace` / `Multiply` / `Add` với `amount`; `useGradientAlpha`.
- **`TextGradient`**: asset preset gradient dùng chung, tự áp lại khi sửa asset.
- **`ColorFlashEffect`**: nhấp nháy về một màu, một lần hoặc lặp, có `charDelay` để tạo vệt quét.
- **Effect tĩnh** (`TextEffect.IsAnimated == false`): chỉ áp một lần, rời vòng update, và áp lại khi TMP dựng lại text. Hiện luôn trong Edit mode (`GradientEffect` không cuộn, `RainbowEffect` có `speed = 0`).
- `TextFX.IsInUpdateLoop`.

### Fixed
- Effect ẩn của tag dựng sẵn bị hiện ra và bị lưu vào scene khi gắn `TextFX` qua Inspector cho text đã có tag (`Undo.AddComponent` reset `hideFlags`).
- Không gỡ được `TextFX` khi còn effect ẩn của tag dựng sẵn. Đã thêm nút **Remove Text FX and its effects** trên Inspector (có trong cả menu ⋮), và trong Edit mode effect ẩn tự bị xóa khi text không còn dùng tag đó.
- Effect lặp có Tag mà text hiện tại không có tag đó thì rời vòng update; nó vào lại khi text có tag.
- Lỗi `IndexOutOfRangeException` / "Mesh.vertices is too small" khi mở scene hoặc reload script trong Edit mode: runner không tick nữa khi TMP đang chờ dựng lại (`havePropertiesChanged`), và tự phát hiện mesh đã được dựng lại mà không có event (số ký tự đổi hoặc buffer vertex bị cấp phát lại).
- Effect bỏ qua ký tự không còn được mesh hiện tại hỗ trợ (`TextEffect.IsDrawable`).
- Sample: 6 mẫu gradient và 5 preset (`Sample/Gradients`: Fire, Ice, Gold, Rainbow, Toxic).
- `WriteAbbreviated(double)` / `SetAbbreviated(double)` cho game idle: đơn vị tới `Vg` (10^63); NaN → `0`, vô cực → `∞`.

### Removed
- **Breaking**: `TextStyle`, `TextStyleLibrary`, `TextStyleApplier` cùng editor và menu "Styled Text". Mọi thuộc tính của chúng đã có trên component TMP; muốn đồng bộ thì dùng material preset, prefab/prefab variant hoặc TMP Style Sheet. Các asset trong `Sample/Styles` cũng bị xóa; component trong scene sample đã được gỡ sạch.

### Changed
- **Tag không còn đổi thành `<link>` của TMP**: preprocessor gỡ tag và ghi lại vị trí. Bỏ được giới hạn không lồng tag, và dùng chung được với `<link>` của người dùng.
- **`TextChangeDispatcher`**: chỉ còn một listener trên event `TEXT_CHANGED` của TMP, chuyển tới đúng text, thay vì mỗi `TextFX` đang thức tự đăng ký riêng.
- **`GradientEffect` dùng LUT** 128 mẫu (gradient kiểu `Fixed` vẫn giữ bậc sắc nét) thay vì gọi `Gradient.Evaluate` cho mỗi vertex.
- `TextFX` chỉ parse lại text khi vừa gán preprocessor (sau khi load hoặc reload), không parse lại mỗi lần bật object trong pool.
- Menu GameObject: thêm **Gradient Text**.
- `TextEffect.Stop()` và việc tắt một effect giờ lập lịch một tick để trả mesh về gốc ngay frame sau.
- **Breaking**: bảng đơn vị rút gọn mới `k, M, B, T, Qa, Qi, Sx, Sp, Oc, No, Dc, Ud, Dd, Td, Qad, Qid, Sxd, Spd, Ocd, Nod, Vg`; nghìn giờ là `k` thường (`15.3k`) thay vì `K`.

## [0.3.0] - 2026-11-03

Mục tiêu của bản này: dùng được cho toàn bộ UI của game (shop, inventory, HUD, popup), và text không chạy effect thì không tốn gì.

### Added
- **`TextFormat`**: định dạng không cấp phát bộ nhớ (zero-GC) vào buffer `char[]`: số có dấu phân cách, rút gọn (`15.3K`, `1.25M`, đến `9.2Qi`), số thập phân, phần trăm, phân số (`3/10`), thời gian (`02:05`, `1:02:05`, `1d 3h`). Có `TimeLabels` cho đa ngôn ngữ và tùy chọn `roundUp` cho đếm ngược.
- **Extension cho `TMP_Text`**: `SetNumber`, `SetAbbreviated`, `SetDecimal`, `SetPercent`, `SetFraction`, `SetTime`, đều có prefix/suffix (dùng được rich text, ví dụ `<sprite name=coin>`).
- **`TextStyleLibrary`**: style theo vai trò (Title, Body, Price...). `TextStyleLibrary.SetCurrent()` để đổi theme cho cả game. Thư viện mặc định đọc từ `Resources/NKL/TextStyleLibrary`.
- `TextStyleApplier`: thêm nguồn **Role** (dropdown chọn role trong Inspector), `SetRole()`, `SetStyle()`, `Resolve()`. Tự áp lại khi asset style/library được sửa trong Editor hoặc khi đổi theme.
- `TextStyle`: các nhóm tùy chọn có công tắc override riêng (màu, font style, auto size, spacing, căn lề). Style chỉ đổi những gì nó quản lý.
- `TextCounter`: chế độ hiển thị `Number` / `Abbreviated` / `Time`, `abbreviateDecimals`, dấu phân cách tùy chỉnh.
- `TextFX.ResetAll()` dùng cho item trong pool hoặc ScrollView, và `TextFX.IsAwake`.
- Menu **GameObject › com.nmd.nkl › Text**: Styled Text, Title (Bounce In), Counter, Typewriter.
- Profiler marker `NKL.TextFX.Tick`.
- **Unit test**: 59 test EditMode (`com.nmd.nkl.20.text.Tests`) cho định dạng, tag, vòng đời effect, typewriter, counter, style.
- **Sample shop** `Sample/ShopSample.unity`: 100 item, giá rút gọn, nút mua (có rung khi thiếu tiền), huy hiệu HOT trong sub-Canvas, coin đếm có animation, đồng hồ sale.

### Changed
- **TextFX khi rảnh không tốn gì**: không có effect đang chạy thì text ra khỏi vòng update và **ngừng nghe event thay đổi text** của TMP. Text chỉ "thức dậy" khi gọi `Play()`. Trước đây mọi `TextFX` đang bật đều bị kiểm tra mỗi frame.
- Khi tắt object, mesh được trả về trạng thái gốc trực tiếp từ bản lưu, không cần `ForceMeshUpdate`.
- `TextCounter` dùng `TextFormat`.
- **Breaking**: `TextCounter.abbreviate`, `useSeparator` (bool) và `separator` (char) được thay bằng `display`, `separator` (string, để trống = không phân cách) và `abbreviateDecimals`.

## [0.2.0] - 2026-11-03

### Added
- **Effect tags**: mỗi effect có trường `Tag`. Viết `<wave>100</wave>` trong text để effect chỉ áp cho đoạn đó. `TextFX` chuyển tag thành `<link="fx:...">` của TMP qua `ITextPreprocessor`, nên không cần tự viết parser rich text.
- Effect mới: `TypewriterEffect`, `FadeInEffect`, `PulseEffect`, `WobbleEffect`, `RainbowEffect`.
- `TypewriterEffect`: tốc độ, fade từng chữ, nghỉ sau dấu câu, `Skip()`, `Show(string)`, event `onCharacterRevealed(int)` (dùng cho âm thanh gõ chữ) và `onComplete`.
- `TextCounter`: số chạy (coin, score) với prefix/suffix, dấu phân cách hàng nghìn, rút gọn 1.2K/3.4M/5B. Không tạo GC khi đếm.
- `TextFX` có thêm follower: `TextExtrude` tự đăng ký lớp khối, nên lớp khối chạy theo effect của chữ chính.
- Preview trong Edit mode: nút **Preview / Stop / Skip** trên Inspector của mọi effect, **Preview All / Stop All** trên `TextFX`.
- Inspector `TextFX` cảnh báo khi effect lặp nằm trong Canvas đông (≥ 20 graphic), có nút **Add sub-Canvas**.
- API: `TextFX.PlayAll()`, `StopAll()`, `IsPlaying`, `AddFollower()`, `RemoveFollower()`, `Reparse()`; `TextEffect.IsLooping`, `TagName`.
- Helper cho effect tự viết: `Rotate`, `TintColor`, `CharacterIndexAt`, `RangeCount`/`GetRange`, `AffectedCount`, `OnPlay`, `OnTextRebuilt`.
- Editor assembly `com.nmd.nkl.20.text.Editor`.
- Scene sample có đủ mọi effect, tag, typewriter, counter, extrude và các nút Replay / Skip / +1234.

### Changed
- Namespace đổi thành `com.nmd.nkl.text`, assembly đổi thành `com.nmd.nkl.20.text`.
- Menu đổi thành `com.nmd.nkl/Text/...`.
- Mỗi frame chỉ copy phần vertex đang dùng (`vertexCount`) thay vì cả buffer.
- Delta time bị giới hạn 0.1 s, để effect không nhảy cóc sau khi game bị treo hoặc chạy nền.
- Runner dùng `DontDestroyOnLoad` thay cho `HideAndDontSave`, nên object runner không còn sót lại sau khi thoát Play mode.

### Fixed
- Lớp `TextExtrude` hiện nguyên tag `<wave>` dưới dạng chữ: lớp này giờ dùng chung preprocessor với chữ chính.

## [0.1.0] - 2026-11-03

### Added
- `TextFX` cùng runner dùng chung: effect chỉ sửa vertex (`UpdateVertexData`), không tạo lại mesh.
- `WaveEffect`, `ShakeEffect`, `BounceInEffect`.
- `TextStyle` (ScriptableObject) và `TextStyleApplier`.
- `TextExtrude`: chữ khối 3D bằng một lớp chữ đồng bộ phía sau.
