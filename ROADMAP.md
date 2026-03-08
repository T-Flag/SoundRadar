# SoundRadar — Roadmap & Research

> Overlay visuel d'accessibilité qui affiche la direction des sons détectés en temps réel.
> Inspiré du mode sourd/malentendant de Fortnite. Projet éducatif en C# / WPF.

---

## État du marché et projets existants

### Solutions natives intégrées aux jeux

**Fortnite — "Visualize Sound Effects"**
- Référence absolue dans le domaine. Affiche des indicateurs directionnels autour du réticule avec des icônes spécifiques par type de son (pas, tirs, coffres, véhicules).
- Avantage majeur : accès direct aux données du moteur de jeu — pas besoin de deviner le type de son, le moteur le sait.
- Depuis 2024, c'est un réglage activé par défaut. Le 3D audio est désactivé quand cette option est active (compromis délibéré).
- Iconographie différenciée : les pas ont un motif gauche/droite, les tirs un éclat orange pixelisé, les coffres un halo jaune. La distance est aussi communiquée visuellement.

**Minecraft — Sous-titres directionnels**
- Système de sous-titres avec flèches directionnelles : "Creeper qui siffle →", "Eau qui coule ←".
- Approche textuelle plutôt que graphique.

**Hunt: Showdown 1896** — Aucune fonctionnalité native d'accessibilité audio.

### Solutions hardware

**Audio Radar (Airdrop Gaming) — 399$**
- Système hardware plug-and-play avec 6 barres LED RGBW autour de l'écran + boîtier central.
- Intercepte le signal HDMI 7.1 surround. Chaque canal correspond à une direction.
- Partenariat officiel avec Logitech G (programme "Start-Up for Good").
- Système de couleurs graduées : vert = loin, jaune = moyen, rouge = proche.
- Limitation actuelle : ne différencie pas les types de sons, tout s'affiche de la même manière.
- Zéro risque anti-cheat (hardware pur, aucun logiciel côté PC).
- Score de performance en test : +47% d'amélioration moyenne vs headset seul (étude sur PUBG Sound Lab).

### Solutions logicielles existantes

**ASUS Sonic Radar III**
- Logiciel propriétaire fourni avec les cartes mères/son ASUS ROG et TUF.
- Traitement audio avancé : filtre et booste des types de sons spécifiques (pas, tirs, voix).
- Affichage HUD 360° avec sensibilité et durée de signal configurables.
- Fonctionne en 7.1 surround pour une vraie spatialisation.
- Limité au hardware ASUS exclusivement.
- Débats récurrents sur la légitimité en compétition.

**CanetisRadar (SamuelTulach) — Open source, C#**
- Application open source utilisant un device audio 7.1 pour calculer la direction des sons.
- Utilise VB-Cable ou Voicemeeter comme device virtuel 7.1.
- Projet archivé, mais de nombreux forks existent.

**CanetisRadar2 (Alaanor) — Open source, C# WPF**
- Créé par une personne sourde de naissance qui entend d'une oreille via technologie.
- Setup 3 écrans : barres sur les écrans latéraux, jeu au centre en fullscreen 165Hz.
- 6 sections : Front/Side/Behind × Left/Right. Code couleur : blanc (silence), jaune (son faible), rouge (son fort).
- Rétention du pic le plus haut pendant un délai configurable.
- **Conclusion de l'auteur** (crucial pour nous) : l'utilisation du volume seul n'est pas une bonne solution. Combat lointain détectable en 3-4 secondes, combat rapproché quasi impossible. L'analyse fréquentielle et le seuil adaptatif sont nécessaires — c'est exactement ce qu'on construit en Phase 2.

**CanetisRadar-Improved (ensingerphilipp) — Open source, C#**
- Fork amélioré avec : support side speakers 7.1, radar sectionné avec highlights, durée de highlight configurable, nombre de sections configurable, seuil minimum de détection, compatibilité plein écran, position configurable.
- Guide complet d'installation avec Voicemeeter Banana.
- Astuce : possibilité d'exclure Discord du radar en routant sur une sortie différente.
- 39 stars, mis à jour jusqu'en août 2025.

**StereoSoundView (Shibi-bala) — Open source**
- Overlay Windows pour visualiser l'audio directionnel gauche/droite.
- Plus simple, stéréo seulement, créé pour résoudre le problème d'un écouteur cassé.

### Enseignements clés

1. **Le 7.1 virtuel est le game changer.** Tous les projets matures utilisent du 7.1, pas du stéréo. 8 canaux = vrai 360°.
2. **Le volume seul ne suffit pas.** Confirmé par l'auteur de CanetisRadar2. L'analyse fréquentielle et le seuil adaptatif sont indispensables.
3. **La classification par type de son est le graal.** Fortnite y arrive nativement, Sonic Radar III avec du traitement propriétaire. Audio Radar hardware n'y arrive pas encore.
4. **La question du cheating est omniprésente.** Même Sonic Radar d'ASUS, livré avec leur hardware, fait débat.

---

## Architecture technique actuelle

### Stack technologique
- **Langage** : C# .NET 8
- **UI** : WPF (overlay transparent, click-through, plein écran)
- **Capture audio** : WASAPI Loopback via NAudio 2.2.1
- **Tests** : xUnit + FluentAssertions (approche TDD)
- **CI/CD** : GitHub

### Modules implémentés (Phase 1 + 2)

```
AudioCaptureService (WASAPI Loopback)
    │
    ├──→ SpectrumAnalyzer (FFT temps réel, fenêtre Hanning)
    │       └──→ FrequencyBandFilter (4 bandes : SubBass/LowMid/Mid/HighMid)
    │               └──→ AdaptiveThreshold (EMA 3-5s, seuil dynamique)
    │                       └──→ SoundEvent enrichi (pan + bande + intensité)
    │
    ├──→ DirectionAnalyzer (pan stéréo L/R, fallback)
    │
    └──→ OverlayWindow (radar circulaire, debug panel, hotkeys globaux)
```

### Bandes de fréquences (calibrées pour Hunt: Showdown)

| Bande | Plage | Sons typiques |
|-------|-------|--------------|
| SubBass | 20-80 Hz | Tonnerre, explosions lointaines |
| LowMid | 80-400 Hz | Corps des pas, impacts |
| Mid | 400-1800 Hz | Définition des pas, surfaces, craquements |
| HighMid | 1800-6000 Hz | Pluie, ambiance, détails |

### Configuration actuelle (calibrée sur vidéos YouTube)

| Paramètre | Valeur | Description |
|-----------|--------|-------------|
| Sensibilité (seuil) | 0.010 | Seuil d'intensité minimum |
| MaxExpectedPan | 0.25 | Normalisation HRTF (pan brut / max attendu) |
| Trigger factor | 1.5 | Facteur multiplicatif du seuil adaptatif |
| Adaptation time | 3.0s | Temps de convergence EMA |

### Raccourcis clavier (globaux, compatibles TKL + Chrome)

| Raccourci | Action |
|-----------|--------|
| Ctrl+Shift+O | Toggle overlay |
| Ctrl+Shift+D | Toggle debug |
| Ctrl+Shift+S | Toggle spectre |
| Ctrl+Shift+Up/Down | Sensibilité +/- |
| Ctrl+Shift+Left/Right | MaxPan +/- 0.05 |
| Ctrl+Shift+Q | Quitter |

---

## Roadmap

### Phase 1 — Fondation ✅
- [x] Capture WASAPI loopback
- [x] Analyse directionnelle stéréo (pan L/R)
- [x] Overlay WPF transparent, click-through, plein écran
- [x] Radar circulaire avec arcs directionnels
- [x] Couleurs distinctes gauche (cyan) / droite (orange)
- [x] Fade-out progressif
- [x] Normalisation pan HRTF (MaxExpectedPan)
- [x] Raccourcis clavier globaux (Ctrl+Shift+...)
- [x] Config persistante (config.json)
- [x] Mode debug complet (panneau temps réel, event log, panneau contrôles)

### Phase 2 — Analyse fréquentielle et filtrage 🔧 En cours
- [x] FFT temps réel (Hanning window)
- [x] Découpage en 4 bandes de fréquences
- [x] Normalisation d'énergie en dB
- [x] Pan par bande de fréquence
- [ ] **Seuil adaptatif fonctionnel** (baseline EMA qui s'adapte au bruit ambiant)
- [ ] Affichage spectre optionnel (Ctrl+Shift+S)
- [ ] Jusqu'à 3 événements simultanés (bandes les plus fortes)
- [ ] Calibrage sur Hunt: Showdown (terrain d'entraînement ou vidéos)

### Phase 3 — Spatialisation 360° via 7.1 virtuel
- [ ] Support Voicemeeter Banana comme device 7.1 virtuel
- [ ] Analyse des 8 canaux (FL, FR, SL, SR, RL, RR, Center, Sub)
- [ ] Mapping angulaire 360° basé sur l'énergie par canal
- [ ] Distinction avant/arrière (impossible en stéréo pur)
- [ ] Mode hybride : stéréo (simple) ou 7.1 (avancé), sélectionnable
- [ ] Guide d'installation Voicemeeter intégré à la documentation
- [ ] Exclusion de sources audio (Discord, Spotify) via routage Voicemeeter

### Phase 4 — Classification des sons
- [ ] Détection de transients (tirs = pic brutal haute fréquence + onset rapide)
- [ ] Analyse par bandes enrichie (pas = énergie 80-800Hz, transitoire modéré)
- [ ] Profil fréquentiel des sons de Hunt (données issues de l'analyse Audacity existante)
- [ ] Icônes distinctes par type de son sur l'overlay (pas, tirs, explosions, ambiance)
- [ ] Code couleur par distance (inspiré Audio Radar : vert = loin, jaune = moyen, rouge = proche)
- [ ] Mode "focus" : filtrer pour n'afficher que certains types (ex: pas + tirs uniquement)
- [ ] Rétention du pic le plus haut pendant un délai configurable (inspiré CanetisRadar2)

### Phase 5 — Widget Xbox Game Bar
- [ ] Réécriture de la couche UI en UWP (le backend audio reste identique)
- [ ] Widget "pinnable" visible par-dessus le jeu
- [ ] Intégration native Windows, whitelisté par EAC
- [ ] Radar compact adapté à la taille d'un widget
- [ ] Réglages accessibles depuis l'interface Game Bar

### Phase 6 — Bonus et polish
- [ ] Profils par jeu (Hunt, Apex, Valorant...)
- [ ] Thèmes visuels personnalisables (couleurs, taille, opacité)
- [ ] Export/import de configuration
- [ ] Mode enregistrement (log des événements sonores pour analyse post-game)
- [ ] Accessibilité : mode daltonien, taille d'éléments ajustable
- [ ] Dashboard de calibrage interactif (joue des sons de test et demande de confirmer la direction)
- [ ] Support multi-écran (overlay sur l'écran de jeu uniquement)

---

## Idées en réserve

### Inspirées des projets existants
- **Système de couleurs par distance** (Audio Radar) : vert/jaune/rouge selon l'intensité pour estimer la proximité
- **Icônes par type de son** (Fortnite) : représentation visuelle immédiatement reconnaissable
- **Radar sectionné** (CanetisRadar-Improved) : sections angulaires distinctes avec highlights
- **Exclusion de sources** (CanetisRadar-Improved) : ne capter que le jeu, pas Discord/Spotify
- **Rétention du pic** (CanetisRadar2) : le signal reste visible au niveau max quelques instants

### Idées originales
- **Widget Game Bar** : aucun projet existant ne l'a fait, résout la question anti-cheat
- **Seuil adaptatif par bande** : le bruit de pluie augmente le seuil HighMid sans affecter LowMid (pas)
- **Profils météo** (issu de notre travail EQ APO) : profil "pluie" qui ajuste les seuils automatiquement
- **IA de classification** (projet long terme) : modèle léger type YAMNet pour identifier les sons de Hunt
- **Mode spectateur** : version simplifiée pour les streams/casting

### Pistes techniques à explorer
- **Ambisonics / HRTF inversée** : reconstituer la direction 3D à partir de l'audio HRTF stéréo
- **Cross-corrélation temporelle** : mesurer le décalage temporel entre L et R pour affiner la direction
- **Spectrogramme temps réel** : visualisation mel-spectrogram pour le debug avancé
- **Machine learning sur spectrogramme** : entraîner un classificateur sur des échantillons de Hunt

---

## Références et ressources

### Projets open source
- [CanetisRadar](https://github.com/SamuelTulach/CanetisRadar) — Original, archivé
- [CanetisRadar2](https://github.com/Alaanor/CanetisRadar2) — Version WPF par développeur sourd
- [CanetisRadar-Improved](https://github.com/ensingerphilipp/CanetisRadar-Improved) — Fork le plus actif (2025)
- [StereoSoundView](https://github.com/Shibi-bala/StereoSoundView) — Overlay stéréo simple

### Produits commerciaux
- [Audio Radar (Airdrop Gaming)](https://audioradar.com/) — Hardware 399$, partenariat Logitech G
- [ASUS Sonic Radar III](https://www.asus.com/microsite/mb/ROG-supremefx-gaming-audio/sonic_radar.html) — Logiciel propriétaire ASUS

### Documentation technique
- [NAudio WASAPI Loopback](https://github.com/naudio/NAudio/blob/master/Docs/WasapiLoopbackCapture.md)
- [Voicemeeter Banana](https://vb-audio.com/Voicemeeter/banana.htm) — Device virtuel 7.1
- [Fortnite Sound Visualizer — Accessibility Labs](https://accessibility-labs.com/feature-highlight-fortnites-sound-visualizer/) — Analyse technique détaillée

### Notre travail préalable
- Profil Equalizer APO pour Hunt: Showdown (5 bandes, optimisé bruits de pas)
- Analyse spectrale Audacity des sons de Hunt (pas, pluie, tonnerre)
- Cartographie fréquentielle : pas 60-2500Hz, pluie 40-8000Hz, tonnerre 5-6000Hz
