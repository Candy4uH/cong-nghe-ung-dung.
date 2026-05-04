# BAO CAO TINH CHINH THAM SO - PHAN 1: scoreThreshold

## 1. Thiet lap thi nghiem (giu nguyen)
- Danh gia tren 60 anh test

## 2. Nhan xet ngan gon (y chinh)
-Đầu tiên ta có thể thấy được rằng khi giảm ngưỡng TH từ 0.72 xuống 0.2 thì tỉ lệ đoán đã giảm từ 3.01 xuống 2.50.
-Vậy nguyên nhân là gì? Ta có thể nhìn thấy rằng chỉ số FP tăng rất mạnh khi giảm TH và nó chính là lý do khiến cho tỉ lệ đoán bị giảm sút.Bởi vì khi hạ TH thì sẽ mở cửa cho nhiều detention hơn những phần lớn là detection sai trong khi TP thì chỉ tăng từ 21->22 dẫn đến tỉ lệ bị giảm .
-Điều này thể hiện rằng đây là tham số có tác động trực tiếp đến mô hình


## PHAN 2: strideRatio

## 1. Ket qua chinh (thay doi strideRatio)
- strideRatio = 0.7 -> Ty le doan tong the = 1.21%
- strideRatio = 0.25 -> Ty le doan tong the = 2.87%

## 2. Nhan xet ngan gon (y chinh)
-Ta thấy được rằng khi giảm strideRatio từ 0.7->0.25 thì tỉ lệ đoán đã được gia tăng rất nhiều,bởi vì khi giảm strideRatio thì mô hình sẽ phát hiện object tốt hơn nhờ vào việc TP tăng từ 11->28
-Lý do là vì giảm strideRatio -> quét dày hơn, giảm khả năng bỏ sót object.Nhưng tốc độ sẽ chậm hơn vì số lượng vị trí quét tăng lên

## PHAN 3: maxTemplatesPerClass
## 2. Nhan xet ngan gon (y chinh)
-Việc tăng maxTemplatesPerClass giúp cho mô hình sẽ có nhiều pattern hơn để so sánh dẫn đến kết quả nhận diện tốt hơn và ta cũng có thể thấy rõ điều đó trong kết quả.
-TP tằng ,FN giảm, Recall tăng và model cũng có sự cải thiện.
-Khi tằng maxTemplatesPerClass thì sẽ dẫn đến kqua nhận diện tốt hơn nhưng cũng phải có sự kiểm soát bởi vì sẽ dễ bị overfit.



