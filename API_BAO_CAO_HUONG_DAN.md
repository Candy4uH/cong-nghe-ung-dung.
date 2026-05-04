# BAO CAO TRIEN KHAI WEB API OBJECT DETECTION

## 1) Muc tieu da hoan thanh
- Da tao API layer de project khac goi model qua HTTP, khong can biet noi bo model.
- Da tach ro Controller va Service theo huong clean.
- Da dinh nghia endpoint theo scope:
- `GET /api/v1/health`
- `GET /api/v1/model/info`
- `POST /api/v1/detection/detect`
- Da chuan hoa response JSON cho detection: `label`, `confidence`, `boundingBox`, `imageWidth`, `imageHeight`.
- Da bat Swagger de test nhanh endpoint.
- Da co validate input co ban va global error handling.

## 2) Kien truc trien khai
- API project: `Object-Detection.Api`
- Core model project: `Object-Detection`
- Luong xu ly:
- Controller nhan request
- Service (`IObjectDetectionService`) xu ly business flow
- `ObjectDetectionRuntime` (bridge) load model serialized + goi inference

## 3) Cac file chinh da them/sua
- `Object-Detection.Api/Program.cs`
- `Object-Detection.Api/Options/ObjectDetectionApiOptions.cs`
- `Object-Detection.Api/Infrastructure/ErrorHandling/ApiException.cs`
- `Object-Detection.Api/Infrastructure/ErrorHandling/GlobalExceptionHandler.cs`
- `Object-Detection.Api/Services/IObjectDetectionService.cs`
- `Object-Detection.Api/Services/ObjectDetectionService.cs`
- `Object-Detection.Api/Controllers/HealthController.cs`
- `Object-Detection.Api/Controllers/ModelController.cs`
- `Object-Detection.Api/Controllers/DetectionController.cs`
- `Object-Detection.Api/Contracts/Responses/*`
- `ObjectDetectionRuntimeBridge.cs` (core bridge de load model va infer)
- `Object-Detection.csproj` (exclude compile cheo tu thu muc API)
- `Object-Detection.Api/Object-Detection.Api.http`

## 4) Validation theo phase (da test)
### Phase 1 - Scaffold API
- Da test: `dotnet build Object-Detection.sln`
- Ket qua: Pass sau khi fix include boundary trong `Object-Detection.csproj`.

### Phase 2 - Runtime bridge
- Da test build solution.
- Da test startup API host thanh cong.

### Phase 3 - Endpoint
- Da test runtime HTTP:
- `GET /api/v1/health` -> `200`, trang thai `degraded` neu model file chua co.
- `GET /api/v1/model/info` -> `503 MODEL_NOT_READY` neu model chua duoc load.
- `POST /api/v1/detection/detect` voi file sai extension -> `400 INVALID_INPUT`.

## 5) Cau hinh can co
Trong `Object-Detection.Api/appsettings.json`:

```json
"ObjectDetection": {
  "ModelFilePath": "model/object-detection-model.json",
  "MaxUploadBytes": 5242880,
  "AllowedExtensions": [".jpg", ".jpeg", ".png", ".bmp", ".webp"]
}
```

Luu y:
- `ModelFilePath` la file model serialized de API load luc runtime.
- Hien tai code da san sang load file nay; ban can dat file model dung schema.

## 6) Huong dan chay API
1. Build solution:

```bash
dotnet build Object-Detection.sln
```

2. Chay API:

```bash
dotnet run --project Object-Detection.Api/Object-Detection.Api.csproj
```

3. Mo Swagger:
- `http://localhost:5161/swagger`

## 7) Hop dong API (de project khac consume)
### 7.1 Health
- `GET /api/v1/health`
- Response mau:

```json
{
  "status": "healthy",
  "modelLoaded": true,
  "message": "Model is ready.",
  "modelFilePath": "D:/.../object-detection-model.json",
  "modelLoadedAtUtc": "2026-04-02T09:20:00+00:00"
}
```

### 7.2 Model info
- `GET /api/v1/model/info`
- Response mau:

```json
{
  "modelName": "FruitDetectorV1",
  "modelVersion": "1.0.0",
  "labels": ["apple", "banana", "orange"],
  "templatesByLabel": { "apple": 120, "banana": 115, "orange": 110 },
  "scoreThreshold": 0.7,
  "scales": [0.8, 1.0, 1.2],
  "thresholdByLabel": { "apple": 0.55 }
}
```

### 7.3 Detect
- `POST /api/v1/detection/detect`
- Content-Type: `multipart/form-data`
- Form field: `image`
- Response mau:

```json
{
  "imageWidth": 1280,
  "imageHeight": 720,
  "processingTimeMs": 48,
  "detections": [
    {
      "label": "apple",
      "confidence": 0.93,
      "boundingBox": {
        "x1": 122,
        "y1": 180,
        "x2": 251,
        "y2": 332
      }
    }
  ]
}
```

## 8) Cach ASP.NET MVC consume API
Dang ky `HttpClient` trong MVC project:

```csharp
builder.Services.AddHttpClient("ObjectDetectionApi", client =>
{
    client.BaseAddress = new Uri("http://localhost:5161/");
});
```

Goi detect endpoint:

```csharp
public async Task<string> DetectAsync(IFormFile file, IHttpClientFactory httpClientFactory)
{
    var client = httpClientFactory.CreateClient("ObjectDetectionApi");

    await using var stream = file.OpenReadStream();
    using var content = new MultipartFormDataContent();
    content.Add(new StreamContent(stream), "image", file.FileName);

    var response = await client.PostAsync("api/v1/detection/detect", content);
    var body = await response.Content.ReadAsStringAsync();

    response.EnsureSuccessStatusCode();
    return body;
}
```

## 9) Ghi chu quan trong
- Scope hien tai khong bao gom train endpoint (theo yeu cau).
- API da su dung abstraction service; logic model noi bo khong lo ra ben ngoai.
- Neu model file chua ton tai, API van chay va tra trang thai ro rang de he thong consume de quan sat.
