# HRIS (ASP.NET Core MVC + SQL Server)

Bu repo, insan kaynakları yönetim sistemi için **ASP.NET Core MVC (.NET 8)** tabanlı bir başlangıç iskeleti içerir.

## Proje Yapısı

- `src/Hris.Web/Controllers` → MVC controller'lar
- `src/Hris.Web/Views` → Razor view'lar
- `src/Hris.Web/Models` → Entity/model sınıfları
- `src/Hris.Web/Data` → `HrisDbContext` ve EF migrations

## Kurulum

1) Bağlantı cümlesi
- `src/Hris.Web/appsettings.json` içindeki `ConnectionStrings:DefaultConnection` değerini kendi SQL Server'ına göre güncelle.
- Varsayılan: LocalDB (Windows)

2) Araçları geri yükle

- `dotnet tool restore`

3) Veritabanını oluştur

- `dotnet tool run dotnet-ef database update --project src/Hris.Web --startup-project src/Hris.Web`

4) Uygulamayı çalıştır

- `dotnet run --project src/Hris.Web`

## Güvenlik Notları (SQL Injection)

- Veri erişimi **EF Core** üzerinden yapıldığı için sorgular parametreli çalışır; string birleştirerek SQL üretme yoktur.
- Eğer ileride ham SQL yazmanız gerekirse:
  - `FromSqlInterpolated` / `ExecuteSqlInterpolated` gibi **parametreli** API'leri kullanın.
  - Asla kullanıcı girdisini string birleştirip SQL'e gömmeyin.

### Preemptive(önleyici) Broken Access Control

 Employee gibi bir kullanıcı eğer endpoint e url girmek isterse aşşağıdaki gibi farklı bir sekmede access denied sayfası yer alır bu sayede yetkisiz
 girişlerin önüne geçmiş oluruz.
 
- <img width="1129" height="330" alt="image" src="https://github.com/user-attachments/assets/d261f846-fdc5-4a43-9c2a-fcbb805d0b7f" />


#### Veri Tabanının gizliliği
Çalışan ve yönetici bilgileri kurum tarafından gizli tutulması gerektiği için SHA-256 token ile veri tabanı bilgileri token olarak saklanır.
<img width="764" height="288" alt="image" src="https://github.com/user-attachments/assets/32999c44-b725-44b2-a999-f91428ef2b87" />



## Hazır Sayfalar

- `/Employees` CRUD
- `/Shifts` CRUD

## 🖥️ Projeden Örnek Sayfalar

---

### Admin Sekmesi

En geniş yetkiye sahip sitemin tümüne hakim kullanıcı yapısıdır.

| Ana Panel | Sol Menü |
|-----------|----------|
| ![](https://github.com/user-attachments/assets/1d135887-f9fe-4e58-9ee3-528efc8fb77b) | ![](https://github.com/user-attachments/assets/5a12f4cf-8be9-4582-aad1-3f684cf87bea) |

---

### HR (İnsan Kaynakları Sekmesi)

Admin ile hemen hemen aynı yetkilere sahip olmasına rağmen admin sekmesinden şifre değiştirme yetkisi yoktur.

| Ana Panel | Sol Menü |
|-----------|----------|
| ![](https://github.com/user-attachments/assets/64793c93-f2c5-4f59-a2b2-c3ffd9fecf4b) | ![](https://github.com/user-attachments/assets/8cf9a9fe-e065-4691-8b35-3774f8e49d01) |

---

### Çalışan Profili (Örnek)
Admin ve İnsan kaynaklarından farklı olarak kendine özgü shift request'lerin olduğu ancak daha kısıtlı bir çalışan yapısıdır.

| Profil Sayfası | Mobil / Dar Görünüm |
|----------------|---------------------|
| ![](https://github.com/user-attachments/assets/daecda64-72b9-41d6-828c-45e0921de495) | ![](https://github.com/user-attachments/assets/87207d47-6ffc-49fe-a431-ea1412c80162) |


## Youtube video
https://www.youtube.com/watch?v=EGO9sOu7WD4




