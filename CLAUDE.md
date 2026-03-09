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
│   │   ├── SoundEvent.cs          # Événement sonore (pan, angle, intensity, decay, DominantBand)
│   │   ├── BandAnalysis.cs        # Résultat analyse par bande (énergie, pan, intensité)
│   │   ├── DebugData.cs           # Données debug + SoundLogEntry (surround support)
│   │   └── AppConfig.cs           # Config JSON persistante (+ SurroundConfig)
│   ├── Analysis/
│   │   ├── DirectionAnalyzer.cs   # Analyse stéréo L/R → pan (-1 à +1)
│   │   ├── SurroundAnalyzer.cs    # Analyse 7.1 → angle 360° (barycentre pondéré)
│   │   ├── SpectrumAnalyzer.cs    # FFT temps réel (Hanning window, FftSharp)
│   │   ├── FrequencyBandFilter.cs # Découpage en 4 bandes fréquentielles
│   │   └── AdaptiveThreshold.cs   # Seuil adaptatif EMA dual-speed + catch-up
│   ├── Audio/AudioCaptureService.cs   # Capture WASAPI loopback via NAudio
│   ├── Overlay/OverlayWindow.xaml(.cs) # Fenêtre transparente click-through + spectre
│   └── App.xaml(.cs)              # Point d'entrée, câblage pipeline
├── SoundRadar.Tests/              # Tests unitaires xUnit (59 tests)
│   ├── SoundEventTests.cs         # 4 tests (decay, expiration, bornes)
│   ├── DirectionAnalyzerTests.cs  # 6 tests (silence, pan L/R/center, seuil)
│   ├── AngleMappingTests.cs       # 4 tests (PanToAngle)
│   ├── PanNormalizationTests.cs   # 5 tests (NormalizePan)
│   ├── SpectrumAnalyzerTests.cs   # 4 tests (FFT peak, silence, output size)
│   ├── FrequencyBandFilterTests.cs # 5 tests (bandes, pan, silence)
│   ├── AdaptiveThresholdTests.cs  # 14 tests (EMA dual-speed, spike, catch-up, convergence)
│   └── SurroundAnalyzerTests.cs   # 12 tests (angle par canal, barycentre, silence, LFE)
```

## Pipeline audio
```
AudioCapture → buffers (stéréo ou 7.1)
  ├──→ SurroundAnalyzer (7.1 → angle 360°, si ≥8 canaux)
  ├──→ DirectionAnalyzer (fallback stéréo, pan L/R)
  └──→ SpectrumAnalyzer (FFT par canal, downmix stéréo si 7.1)
        └──→ FrequencyBandFilter (énergie/pan par bande)
              └──→ AdaptiveThreshold (filtrage par bande)
                    └──→ SoundEvent enrichi (angle/pan + top 3 bandes)
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
- `Ctrl+Shift+D` : Toggle debug
- `Ctrl+Shift+A` : Cycle adapt time (0.5 / 1.5 / 3.0s)
- `Ctrl+Shift+N` : Cycle noise floor (-60 / -40 / -20 dB)
- `Ctrl+Shift+Q` : Quitter

## Commandes utiles
```bash
dotnet build
dotnet test      # 59 tests
dotnet run --project SoundRadar
```

## Conventions
- TDD : écrire les tests avant l'implémentation pour Models et Analysis
- Pas de TDD pour les couches hardware (Audio) et UI (Overlay)
- FluentAssertions 8.x : `BeGreaterThanOrEqualTo` / `BeLessThanOrEqualTo`
- Le projet de tests doit cibler `net8.0-windows` pour référencer le projet WPF

## Seuil adaptatif (AdaptiveThreshold)
- EMA dual-speed : alphaFast pour le bruit ambiant, alphaSlow (10× plus lent) pour les spikes
- Catch-up : après 20 frames consécutives de spike (~0.33s), bascule en alphaFast (le "spike" est un nouveau niveau ambiant)
- Calibration par défaut : adaptationTimeSec=0.5, triggerFactor=1.5, noiseFloorDb=-60

## Surround 7.1 (SurroundAnalyzer)
- Barycentre pondéré : angle = atan2(Σ energy×sin(θ), Σ energy×cos(θ))
- 7 canaux directionnels : FL(-45°), FR(+45°), FC(0°), SL(-90°), SR(+90°), RL(-135°), RR(+135°)
- LFE (canal 3) ignoré — pas directionnel
- Auto-détection : si WASAPI device ≥ 8 canaux, mode surround activé automatiquement
- Downmix FL+FR vers stéréo pour le pipeline FFT existant

## État du projet
- **Phase 1 terminée** : capture audio, analyse directionnelle, overlay
- **Phase 2 terminée** : FFT, bandes fréquentielles, seuil adaptatif, spectre, calibrage
- **Phase 3 terminée** : support 7.1 surround, radar 360°, debug per-channel
- 59/59 tests passent
- Repo GitHub : https://github.com/T-Flag/SoundRadar

## Git
- GitHub : T-Flag
- User : T-Falanga
- Email : thomas.falanga.1@gmail.com
