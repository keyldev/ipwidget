# IP Widget

Лёгкий кроссплатформенный виджет для проверки внешнего IP на **Avalonia / .NET 9**.

<p align="center">
  <em>Стеклянный фреймлесс-виджет · проверка по 6 источникам · геолокация · детект VPN/хостинга · трей</em>
</p>

## Возможности

- **6 источников параллельно** — ipify, ifconfig.me, icanhazip, ipinfo.io, seeip, checkip.amazonaws. Итоговый IP берётся по большинству голосов, так что один упавший эндпоинт не врёт.
- У каждого источника свой статус (ожидание → успех/ошибка) и **латентность в мс**.
- **Геолокация + провайдер/ASN** через [ipwho.is](https://ipwho.is) — страна, город, ISP, номер автономной системы.
- **Эвристика VPN / хостинга** — по фингерпринтам ISP/org/domain помечает датацентр-адреса.
- **Трей** — сворачивание в трей, меню *Показать / Обновить / Выход*.
- Вайбовая внешка: AcrylicBlur, градиент, неоновое свечение, векторные иконки (MDI).
- Поверх всех окон, копирование IP в буфер, автообновление раз в 30 с.

## Запуск

```bash
dotnet run -c Release
```

## Сборка single-file

```bash
# win-x64 / linux-x64 / osx-arm64 — меняется только -r
dotnet publish -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

## Стек

- [Avalonia UI](https://avaloniaui.net) 11.2 · .NET 9
- Без внешних ассетов — иконка приложения/трея рисуется в рантайме.

## Релизы

Пуш тега `v*` собирает через GitHub Actions single-file бинарники под
`win-x64`, `linux-x64`, `osx-x64`, `osx-arm64` и прикладывает их к релизу.

```bash
git tag v0.1.0 && git push origin v0.1.0
```

## Лицензия

[MIT](LICENSE) © keyldev
