# Object-Detection Project Analysis

Tài liệu này mô tả toàn bộ dự án theo từng câu hỏi, để bạn nắm chắc: bài toán, dữ liệu, mô hình, pipeline, thông số, metric, giới hạn và workflow.

## 1) Bài toán của chương trình đang giải là gì?
Chương trình đang giải bài toán **object detection cơ bản** trên ảnh:
- Đầu vào: ảnh + dữ liệu huấn luyện (ảnh đã được annotation).
- Đầu ra: các bounding box dự đoán, nhãn (label), điểm tin cậy (score).

Kiểu bài toán:
- Không phải deep learning end-to-end.
- Đây là baseline detector theo hướng **template histogram màu + sliding window**.

## 2) Input và output của model
### Input (lúc train)
- Danh sách annotation XML trong thư mục train.
- Mỗi XML chứa: tên ảnh + danh sách object (label + bbox).

### Input (lúc test/predict)
- Ảnh test (từ tập test) hoặc 1 ảnh truyền qua `--predictImage`.

### Output
- Mỗi detection:
  - `label` (vd: apple, banana, orange)
  - `score` (0..1)
  - `bbox = (x1,y1,x2,y2)`
- Lúc test, chương trình in:
  - TP, FP, FN
  - Precision, Recall, F1
  - PredictionRate

## 3) Dataset gồm những gì? Annotation chứa gì?
Dự án sử dụng format annotation kiểu Pascal VOC XML.

Cấu trúc dữ liệu:
- `train/`: annotation train (`*.xml`)
- `test/`: annotation test (`*.xml`)
- Ảnh được tìm theo tên trong XML (có thể ở cùng folder hoặc được index toàn workspace).

Trong mỗi XML, các trường quan trọng:
- `filename`: tên file ảnh
- `object` (lặp lại cho mỗi vật thể)
  - `name`: label class
  - `bndbox`
    - `xmin`, `ymin`, `xmax`, `ymax`

## 4) Đọc dataset như thế nào?
Có 2 lớp phối hợp:

1. `AnnotationLoader.Load(...)`
- Liệt kê file XML trong thư mục.
- Giới hạn số lượng theo `maxImages`.
- Parse từng XML thành `AnnotatedImage`.

2. `ImageLocator.Find(...)`
- Tìm file ảnh theo `filename`:
  - Thử trực tiếp trong folder annotation
  - Thử ở root dir
  - Thử đổi extension (`.jpg/.png/.jpeg`)
  - Nếu vẫn không thấy, tạo index tất cả ảnh trong workspace để tìm theo tên file.

## 5) Mô tả toàn bộ vai trò các file trong dự án
### File nguồn chính
- `Program.cs`
  - Entry point console.
  - Điều phối train -> test/evaluate -> predict ảnh đơn.

- `DetectorConfig.cs`
  - Parse tham số CLI.
  - Tự động resolve đường dẫn train/test.

- `AnnotationLoader.cs`
  - Đọc Pascal VOC XML.
  - Chuyển XML thành object data sử dụng trong code.

- `ImageLocator.cs`
  - Tìm file ảnh thực tế cho mỗi annotation.

- `DataModels.cs`
  - Các record data trung tâm: bbox, detection, model, metric record.

- `BasicHistogramDetector.cs`
  - Train template histogram.
  - Detect bằng sliding window + similarity + NMS.

- `Evaluator.cs`
  - Match detection với ground truth theo IoU.
  - Tính TP/FP/FN và metric tổng hợp.

- `Geometry.cs`
  - Hàm IoU cho bbox.

### File cấu hình/build
- `Object-Detection.csproj`
  - Cấu hình .NET 9.
  - Khai báo package OpenCvSharp.

- `Object-Detection.sln`
  - Solution file của Visual Studio/.NET.

### Thư mục phát sinh
- `bin/`, `obj/`
  - File build output và artifact tạm (không phải logic nghiệp vụ).

### Thư mục dữ liệu
- `train/`, `test/`
  - Annotation XML (và ảnh liên quan).

## 6) Từng khối và từng hàm trong từng file
## `Program.cs`
- `Main(string[] args)`
  - Tạo config từ CLI.
  - Validate train/test dir.
  - Load train/test.
  - Train model.
  - Detect trên test, in metric từng ảnh.
  - Tổng kết metric.
  - Nếu có `predictImage` thì gọi predict ảnh đơn.

- `PredictSingleImage(...)`
  - Đọc 1 ảnh.
  - Chạy detect.
  - In danh sách kết quả `label + tỉ lệ` và top-1.

## `DetectorConfig.cs`
- `FromArgs(string[] args)`
  - Parse cặp `--key value` từ command line.
  - Convert sang kiểu dữ liệu phù hợp.
  - Đặt default value.

- `ToString()`
  - In config để theo dõi lúc run.

- `ResolveDefaultDataDir(...)`
  - Tự động tìm train/test theo CurrentDirectory và BaseDirectory.

- `FindFolderInParents(...)`
  - Đi ngược lên parent folders để tìm folder mong muốn.

## `AnnotationLoader.cs`
- `Load(...)`
  - Đọc danh sách XML, parse từng file.

- `ParseOne(...)`
  - Parse 1 XML Pascal VOC.
  - Đọc filename, object list, bbox.

- `ParseInt(...)`
  - Parse int an toàn.

## `ImageLocator.cs`
- `Find(...)`
  - Tìm đường dẫn ảnh từ nhiều candidate.

- `BuildFileIndex(...)`
  - Tạo bảng tra tên file ảnh -> full path trong workspace.

## `DataModels.cs`
- `BoundingBox`
  - Lưu label + toạ độ GT box.

- `Detection`
  - Lưu label + toạ độ box dự đoán + score.

- `AnnotatedImage`
  - 1 ảnh + danh sách GT boxes.

- `TemplateFeature`
  - Template histogram cho 1 mẫu train.

- `DetectorModel`
  - Model sau train:
    - templates
    - avg object size theo label
    - prototype histogram theo label

- `EvalResult`
  - Lưu TP/FP/FN.
  - Tính Precision/Recall/F1/PredictionRate.

## `BasicHistogramDetector.cs`
- `Train(...)`
  - Lặp theo epoch.
  - Shuffle train.
  - Cắt ROI theo bbox.
  - Tạo histogram (gốc + augmentation) làm template.
  - Tổng hợp avg size và prototype.

- `Detect(...)`
  - Group templates theo label.
  - Tạo cửa sổ trượt theo scale + stride.
  - Tính similarity của ROI với template/prototype.
  - Lọc theo threshold.
  - NMS và cắt top detection.

- `ExtractHistogram(...)`
  - Chuyển BGR -> HSV.
  - Tính histogram HxS (16x16) và normalize.

- `ExtractAugmentedHistograms(...)`
  - Epoch >= 2: thêm flip ngang.
  - Epoch >= 3: thêm jitter độ sáng/tương phản.

- `BuildPrototypes(...)`
  - Lấy trung bình histogram theo label.

- `CombinedSimilarity(...)`
  - Điểm cuối = mix(best template, prototype) với `PrototypeWeight` nội bộ.

- `CosineSimilarity(...)`
  - Đo độ giống nhau của 2 vector histogram.

- `ClampRect(...)`
  - Chuẩn hoá bbox để không vượt biên ảnh.

- `NonMaximumSuppression(...)`
  - Loại box trùng lặp theo IoU threshold.

## `Evaluator.cs`
- `EvaluateImage(...)`
  - Match detection và ground-truth cùng label theo IoU cao nhất.
  - Mỗi GT chỉ được match 1 lần.
  - Tính TP/FP/FN cho ảnh.
  - Cộng dồn vào tổng.

- `GetSummary()`
  - Lấy tổng TP/FP/FN của cả tập test.

## `Geometry.cs`
- `IoU(...)` overloads
  - Tính IoU giữa 2 box (detection-detection hoặc detection-GT).

## 7) Chương trình dự đoán object bằng cách nào? Có dùng thuật toán/kỹ thuật gì?
Có. Chương trình dùng các kỹ thuật classic computer vision (không dùng deep learning):

1. **Feature**: HSV color histogram
- Mỗi ROI được biểu diễn thành vector histogram màu.

2. **Template matching theo cosine similarity**
- So ROI với nhiều template đã học của từng label.

3. **Prototype matching**
- Mỗi label có 1 prototype histogram trung bình.
- Điểm dự đoán là kết hợp giữa template tốt nhất và prototype.

4. **Sliding Window**
- Quét toàn ảnh theo cửa sổ trượt và nhiều scale.

5. **NMS (Non-Maximum Suppression)**
- Loại bỏ nhiều bbox trùng nhau.

6. **Evaluation bằng IoU matching**
- Match detection với GT bằng IoU threshold.

## 8) Sử dụng package gì và vai trò của nó?
Trong `Object-Detection.csproj`:

- `OpenCvSharp4`
  - API OpenCV cho .NET.
  - Dùng để đọc ảnh, chuyển màu, xử lý ma trận ảnh.

- `OpenCvSharp4.runtime.win`
  - Runtime native OpenCV cho Windows.
  - Cần để code OpenCvSharp chạy được.

Ngoài ra dùng thư viện .NET có sẵn:
- `System.Xml.Linq` để đọc XML.
- `System.Globalization` để parse số từ CLI.

## 9) Pipeline của chương trình
1. Parse config từ CLI.
2. Resolve/train-test paths.
3. Load train annotations + map ảnh.
4. Train detector:
   - tạo templates histogram
   - tạo prototype theo label
   - tính avg object size theo label
5. Load test annotations.
6. Detect trên từng ảnh test:
   - sliding window + scales
   - similarity scoring
   - threshold
   - NMS
7. Evaluate:
   - TP/FP/FN
   - Precision/Recall/F1/PredictionRate
8. (Optional) Predict 1 ảnh riêng qua `--predictImage`.

## 10) Vai trò từng tham số khi train/predict
Các tham số CLI hiện có:

- `--trainDir`
  - Thư mục annotation train.

- `--testDir`
  - Thư mục annotation test.

- `--predictImage`
  - Đường dẫn ảnh dự đoán thêm sau khi test.

- `--epochs`
  - Số vòng lặp train template.
  - Epoch cao hơn -> có thêm augmentation -> tăng độ đa dạng template.

- `--maxTemplatesPerClass`
  - Trần template cho mỗi label.
  - Tăng lên có thể tăng recall, nhưng cũng có thể tăng FP và chậm hơn.

- `--maxTrainImages`
  - Số annotation train được nạp.
  - Nhỏ quá có thể làm model lệch class.

- `--scales`
  - Danh sách scale của sliding window.
  - Nhiều scale hơn -> bắt size đa dạng hơn, nhưng chậm hơn.

- `--strideRatio`
  - Độ bước nhảy của cửa sổ trượt.
  - Nhỏ hơn -> quét kỹ hơn (thường recall tăng), nhưng chậm hơn.

- `--scoreThreshold`
  - Ngưỡng score để giữ detection.
  - Cao -> giảm FP, dễ tăng miss (FN).
  - Thấp -> tăng recall, dễ tăng FP.

- `--nmsIou`
  - Ngưỡng NMS để loại box trùng.
  - Thấp hơn -> loại mạnh tay hơn.

Lưu ý: trong code có ngưỡng adaptive theo epoch:
- `finalThreshold = clamp(scoreThreshold - 0.02*(epochs-1), 0.35, 1.0)`

## 11) Các chỉ số đánh giá
Từ `EvalResult`:

- `TP` (True Positive): dự đoán đúng object.
- `FP` (False Positive): dự đoán nhầm.
- `FN` (False Negative): bỏ sót object.

Công thức:
- `Precision = TP / (TP + FP)`
- `Recall = TP / (TP + FN)`
- `F1 = 2 * Precision * Recall / (Precision + Recall)`
- `PredictionRate = TP / (TP + FP + FN)`

`PredictionRate` là metric tự thêm trong dự án (không phải metric chuẩn phổ biến như mAP).

## 12) Chương trình có những giới hạn gì?
1. Không dùng deep feature -> khó xử lý background phức tạp, đổi màu, blur, xoay, occlusion.
2. Phụ thuộc mạnh vào màu (HSV histogram), ít nhạy với hình dáng.
3. Sliding window rất tốn thời gian khi ảnh lớn/nhiều scales.
4. Chưa có mAP, AP theo class, confusion matrix.
5. Đang đánh giá theo annotation XML; nếu XML/ảnh sai map sẽ bỏ qua mẫu.
6. Cân bằng class dễ bị lệch nếu train subset theo thứ tự tên file.
7. Ngưỡng và hệ số đang tune tay, chưa có auto-search.

## 13) Workflow để vận hành chương trình
### Workflow cơ bản
1. Chuẩn bị `train/` và `test/` (XML + ảnh).
2. Chạy:
   - `dotnet run -- --maxTrainImages 300 --epochs 6 --maxTemplatesPerClass 150 --scoreThreshold 0.5 --strideRatio 0.5 --scales 0.8,1.0,1.2 --nmsIou 0.3`
3. Xem tổng kết Precision/Recall/F1/PredictionRate.
4. Điều chỉnh tham số:
   - Nếu FP cao: tăng `scoreThreshold`, giảm scales, siết `nmsIou`.
   - Nếu FN cao: giảm `scoreThreshold`, giảm `strideRatio`, tăng `maxTemplatesPerClass`, tăng `maxTrainImages`.

### Workflow predict 1 ảnh
1. Train+test như trên hoặc giữ nhẹ.
2. Thêm `--predictImage "<đường_dẫn_ảnh>"`.
3. Đọc output `label + tỉ lệ + box`, quan sát top-1.

---
Chỉ số chọn để tinh chỉnh:
scoreThreshold
strideRatio
maxTemplatesPerClass


## Kết luận
Đây là một baseline object detection theo hướng classic CV: dễ hiểu, dễ tune, chạy console gọn, phù hợp mục tiêu học tập và đồ án cơ bản.

Nó hoạt động vì:
- model học "màu sắc đặc trưng" từ bbox train,
- quét cửa sổ trên ảnh mới,
- so độ giống với template/prototype,
- lọc kết quả bằng threshold + NMS,
- và đánh giá bằng IoU để ra TP/FP/FN/F1.

Nếu bạn muốn nâng cấp tiếp theo hướng nghiêm túc hơn, bước tiếp theo hợp lý là:
- thêm metric mAP,
- cân bằng sampling theo class,
- và chuyển sang detector deep learning (YOLO/SSD) để tăng độ chính xác thực tế.
