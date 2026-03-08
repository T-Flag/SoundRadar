# SoundRadar — Guide projet

## Description
Overlay visuel d'accessibilité affichant la direction des sons en temps réel (gauche/droite). Inspiré du mode sourd/malentendant de Fortnite. Projet éducatif en C# / WPF.

## Stack technique
- .NET 8 / WPF (net8.0-windows)
- NAudio 2.2.1 (capture audio WASAPI loopback)
- FftSharp 2.2.0 (FFT temps réel)
- xUnit + FluentAssertions 8.8.0 (tests)
- Windows 11

## Structure du projet
```
SoundRadar.sln
├── SoundRadar/                    # Projet WPF principal
│   ├── Models/
│   │   ├── SoundEvent.cs          # Événement sonore (pan, intensity, decay, DominantBand)
│   │   ├── BandAnalysis.cs        # Résultat analyse par bande (énergie, pan, intensité)
│   │   └── AppConfig.cs           # Config JSON persistante
│   ├── Analysis/
│   │   ├── DirectionAnalyzer.cs   # Analyse stéréo L/R → pan (-1 à +1)
│   │   ├── SpectrumAnalyzer.cs    # FFT temps réel (Hanning window, FftSharp)
│   │   ├── FrequencyBandFilter.cs # Découpage en 4 bandes fréquentielles
│   │   └── AdaptiveThreshold.cs   # Seuil adaptatif EMA par bande
│   ├── Audio/AudioCaptureService.cs   # Capture WASAPI loopback via NAudio
│   ├── Overlay/OverlayWindow.xaml(.cs) # Fenêtre transparente click-through + spectre
│   └── App.xaml(.cs)              # Point d'entrée, câblage pipeline
├── SoundRadar.Tests/              # Tests unitaires xUnit (33 tests)
│   ├── SoundEventTests.cs         # 4 tests (decay, expiration, bornes)
│   ├── DirectionAnalyzerTests.cs  # 6 tests (silence, pan L/R/center, seuil)
│   ├── AngleMappingTests.cs       # 4 tests (PanToAngle)
│   ├── PanNormalizationTests.cs   # 5 tests (NormalizePan)
│   ├── SpectrumAnalyzerTests.cs   # 4 tests (FFT peak, silence, output size)
│   ├── FrequencyBandFilterTests.cs # 5 tests (bandes, pan, silence)
│   └── AdaptiveThresholdTests.cs  # 5 tests (seuil adaptatif, spike, EMA)
```

## Pipeline audio
```
AudioCapture → buffers
  ├──→ DirectionAnalyzer (fallback, pan global)
  └──→ SpectrumAnalyzer (FFT par canal)
        └──→ FrequencyBandFilter (énergie/pan par bande)
              └──→ AdaptiveThreshold (filtrage par bande)
                    └──→ SoundEvent enrichi (top 3 bandes)
```

## Bandes de fréquences
- SubBass : 20-80 Hz (tonnerre, explosions)
- LowMid : 80-400 Hz (pas, impacts)
- Mid : 400-1800 Hz (définition des pas, surfaces)
- HighMid : 1800-6000 Hz (pluie, ambiance, détails)

## Raccourcis globaux
- `Ctrl+Shift+O` : Toggle overlay
- `Ctrl+Shift+Up/Down` : Sensibilité ±
- `Ctrl+Shift+Left/Right` : Pan range ±
- `Ctrl+Shift+S` : Toggle spectre
- `Ctrl+Shift+Q` : Quitter

## Commandes utiles
```bash
dotnet build
dotnet test      # 33 tests
dotnet run --project SoundRadar
```

## Conventions
- TDD : écrire les tests avant l'implémentation pour Models et Analysis
- Pas de TDD pour les couches hardware (Audio) et UI (Overlay)
- FluentAssertions 8.x : `BeGreaterThanOrEqualTo` / `BeLessThanOrEqualTo`
- Le projet de tests doit cibler `net8.0-windows` pour référencer le projet WPF

## État du projet
- **Phase 1 terminée** : capture audio, analyse directionnelle, overlay
- **Phase 2 terminée** : FFT, bandes fréquentielles, seuil adaptatif, spectre
- 33/33 tests passent
- Repo GitHub : https://github.com/T-Flag/SoundRadar

## Git
- GitHub : T-Flag
- User : T-Falanga
- Email : thomas.falanga.1@gmail.com
