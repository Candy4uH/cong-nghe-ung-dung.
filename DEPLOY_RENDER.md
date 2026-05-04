# Deploy API len Render

Du an nay da co san Web API ASP.NET Core, nen de nop "bai 3 + bai 4" ban khong can viet lai bang Flask/FastAPI. Dieu can chung minh la:

- Co API nhan anh va tra JSON bbox
- API do chay online tren cloud voi URL public

## 1. Endpoint hien co

- Health: `GET /api/v1/health`
- Model info: `GET /api/v1/model/info`
- Predict: `POST /api/v1/detection/detect`

API predict nhan `multipart/form-data` voi field ten `Image`.

## 2. Kiem tra local truoc khi deploy

Tu thu muc `Object-Detection`:

```powershell
dotnet run --project Object-Detection.Api/Object-Detection.Api.csproj
```

Mo Swagger:

```text
http://localhost:5161/swagger
```

Hoac goi API bang `curl`:

```bash
curl -X POST "http://localhost:5161/api/v1/detection/detect" \
  -F "Image=@test/apple_78.jpg"
```

## 3. Day code len GitHub

1. Tao repository GitHub.
2. Push toan bo source len repo.
3. Dam bao trong repo co cac file:
   - `Object-Detection/Dockerfile`
   - `Object-Detection/render.yaml`
   - `Object-Detection/model/object-detection-model.json`

## 4. Deploy len Render

Theo tai lieu Render, ban co the deploy truc tiep tu GitHub va Render ho tro build tu `Dockerfile`.

1. Vao `https://render.com`
2. Chon `New +` -> `Blueprint`
3. Connect GitHub repo
4. Render se doc file `render.yaml`
5. Bam `Apply`

Render se build container va tao web service. Health check duoc cau hinh san:

```text
/api/v1/health
```

Sau khi deploy xong, ban se nhan duoc URL dang:

```text
https://object-detection-api.onrender.com
```

## 5. Goi API sau khi deploy

Swagger:

```text
https://your-service-name.onrender.com/swagger
```

Health:

```text
https://your-service-name.onrender.com/api/v1/health
```

Predict:

```bash
curl -X POST "https://your-service-name.onrender.com/api/v1/detection/detect" \
  -F "Image=@test/apple_78.jpg"
```

## 6. Cach mo ta trong bao cao

Ban co the viet ngan gon:

1. He thong da xay dung API object detection bang ASP.NET Core Web API.
2. API nhan anh qua HTTP `multipart/form-data`, chay model, va tra ve JSON gom nhan, do tin cay, va bounding box.
3. He thong da duoc dong goi bang Docker.
4. API da duoc trien khai len Render cloud va co URL public de client goi tu xa.

## 7. Luu y quan trong cho bai nop

- Neu giang vien bat buoc Python `Flask/FastAPI` thi du an nay chua dung framework do.
- Neu yeu cau chi la "wrap model thanh API va deploy cloud" thi du an nay dat yeu cau.
- Model hien tai khong phai YOLO/SSD/Faster R-CNN. Day la detector tu xay dung bang OpenCV + histogram.
