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

## Hazır Sayfalar

- `/Employees` CRUD
- `/Shifts` CRUD

- ## Projeden örnek sayfalar
- 
- ### Admin Sekmesi
- <img width="1892" height="863" alt="image" src="https://github.com/user-attachments/assets/738c5c74-4911-4b2f-a10a-3b5eaca21bc7" />


- <img width="1886" height="818" alt="image" src="https://github.com/user-attachments/assets/fd5e1c94-462b-4d16-a341-63f202fc6b31" />

### Çalışan Profili
- <img width="1903" height="843" alt="image" src="https://github.com/user-attachments/assets/20e27c7f-bb99-4ca3-b730-899bedb4ee00" />

## Youtube video
https://www.youtube.com/@ahmetergin9782/posts





