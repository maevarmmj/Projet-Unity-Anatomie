# 🧠 Projet Unity AR – Visualisation Anatomique en Réalité Augmentée

## 🎯 Objectif général

Développer une **application de Réalité Augmentée (AR)** sur **tablette Samsung Tab S7 FE (ARCore)** permettant de :

- Filmer une personne en direct avec la caméra.
- **Superposer des couches anatomiques 3D** (os, muscles, organes, peau) **suivant ses mouvements en temps réel**.
- Permettre à l’utilisateur de **afficher/masquer les couches** via une interface simple et interactive.
- Intégrer un **mini-jeu pédagogique** autour de l’anatomie humaine.
- (Bonus) Rendre l’application compatible avec le **squelette physique “Carlos”** de la salle E-Santé.

---

## 👥 Équipe (6 personnes)

| Rôle | Missions principales |
|------|-----------------------|
| **Chef de projet / Intégrateur** | Planification, gestion Git/GitLab, coordination des sprints, suivi des livrables, présentation. |
| **Développeur AR (Lead)** | Intégration AR Foundation / ARCore, pipeline caméra, placement et affichage des modèles 3D, build Android. |
| **Ingénieur Pose / ML** | Estimation de pose en temps réel (YOLO-Pose, BlazePose), extraction des keypoints, mapping vers le modèle 3D. |
| **Artiste 3D / Rigger** | Recherche et préparation de modèles anatomiques (os, muscles, organes, peau), rigging, LOD, optimisation. |
| **Développeur UI / Mini-jeu** | Interface utilisateur, gestion des couches (toggle), mini-jeu pédagogique, transitions, scoring. |
| **QA / Performance / Documentation** | Tests fonctionnels et de performance, stabilité, FPS, documentation technique et utilisateur. |

---

## 🗓️ Phases de développement

### Phase 0 — Kickoff & Préparation (2–4 jours)
**Objectifs :**
- Définir les livrables, répartir les rôles et installer l’environnement.
- Créer le dépôt Git, la structure du projet et le backlog.

**Sous-objectifs :**
- Créer un dépôt GitHub/GitLab avec branches `main`, `dev`, `feature/*`.
- Mettre en place un tableau Kanban (`To Do`, `In Progress`, `Review`, `Done`).
- Vérifier la compatibilité ARCore de la tablette.
- Créer un planning de sprints (2 à 3 sprints de 2 semaines).

---

### Phase 1 — Prototype rapide (POC) (1–2 semaines)
**Objectif :**
Obtenir une démonstration minimale : caméra AR + squelette 3D approximativement aligné à la personne.

**Sous-objectifs :**
- Configurer Unity (LTS) avec **AR Foundation** et **ARCore XR Plugin**.
- Afficher le **flux caméra** sur tablette.
- Implémenter la **détection de pose** (YOLO-pose / BlazePose).
- Importer un modèle 3D simple (squelette) dans Unity.
- Mapper grossièrement les keypoints 2D → modèle 3D.
- Première démo interne sur tablette.

**Livrable :**
> Une personne filmée, un squelette 3D qui suit globalement ses mouvements.

---

### Phase 2 — Amélioration du tracking et du retargeting (2–3 semaines)
**Objectif :**
Stabiliser le suivi et faire correspondre correctement la pose humaine au modèle 3D riggé.

**Sous-objectifs :**
- Lissage des keypoints (filtre Kalman / moyenne glissante).
- Gestion des occlusions (confidence threshold).
- Retargeting IK : appliquer les rotations/positions sur le rig.
- Ajustement de l’échelle selon la hauteur estimée de l’utilisateur.
- Découpage du modèle en **couches anatomiques** indépendantes (os, muscles, organes, peau).

**Livrable :**
> Les couches anatomiques suivent les mouvements de la personne avec fluidité.

---

### Phase 3 — Interface utilisateur et gestion des couches (1–2 semaines)
**Objectif :**
Créer une interface fluide et tactile permettant d’afficher/masquer les couches anatomiques.

**Sous-objectifs :**
- Conception du panneau latéral / overlay flottant avec :
  - Toggles pour chaque couche.
  - Sliders pour transparence.
  - Boutons `Reset`, `Recalibrate`.
- Implémentation des transitions (fade in/out).
- Test UX sur tablette (ergonomie, taille des boutons).
- Mode “Carlos” : bouton pour basculer sur calibration squelette physique.

**Livrable :**
> UI fonctionnelle et intuitive intégrée dans l’application.

---

### Phase 4 — Mini-jeu pédagogique (1–2 semaines)
**Objectif :**
Créer un mini-jeu pour tester les connaissances anatomiques.

**Idées :**
- **Quiz AR** : trouver une structure à l’écran.
- **Drag & Drop** : placer le nom de l’organe sur la bonne zone.
- **Challenge de rapidité** : identifier X éléments en un temps donné.

**Sous-objectifs :**
- Définir règles et scoring.
- Créer feedback visuel et audio.
- Intégrer le mini-jeu à l’UI principale.
- Ajouter un écran de score final.

**Livrable :**
> Mini-jeu complet, jouable depuis le menu principal.

---

### Phase 5 — Mode “Carlos” (1 semaine)
**Objectif :**
Faire fonctionner le modèle sur le squelette physique présent dans la salle E-Santé.

**Sous-objectifs :**
- Calibration du modèle 3D sur le squelette réel via gommettes ou repères.
- Ajustement échelle et alignement.
- Gestion d’un mode “objet fixe”.

**Livrable :**
> Mode “Carlos” fonctionnel et calibré.

---

### Phase 6 — Optimisation, tests et packaging (1–2 semaines)
**Objectif :**
Préparer la version finale stable et fluide.

**Sous-objectifs :**
- Optimiser performances (LOD, draw calls, textures, mémoire).
- Tests sur plusieurs utilisateurs (latence, stabilité).
- Génération du build final `.apk`.
- Rédaction de la documentation finale et de la présentation vidéo.

**Livrable :**
> APK final + rapport technique + vidéo de démonstration.

---

## 🧩 Organisation du travail en parallèle

| Personne | Tâches parallèles possibles | Période |
|-----------|-----------------------------|----------|
| **Dev AR** | Setup Unity + caméra AR + intégration 3D | dès semaine 1 |
| **Ingénieur Pose** | Pose detection (YOLO/BlazePose), script keypoints | dès semaine 1 |
| **Artist 3D** | Recherche modèles, rigging, textures, couches | dès semaine 1 |
| **Dev UI** | Maquettes UI + menu couches | dès semaine 1 |
| **QA** | Plan de tests + check build tablette | dès semaine 1 |
| **Chef de projet** | Gestion Git, suivi sprints, planification | continu |

> ⚙️ Dès la fin de la semaine 2 : intégration entre tracking, modèles 3D et UI.

---

## 📦 Technologies et outils

- **Moteur** : Unity (LTS)
- **Langage** : C#
- **AR SDK** : AR Foundation + ARCore XR Plugin
- **Estimation de pose** : YOLO-pose ou BlazePose
- **Formats 3D** : FBX / glTF
- **Pipeline rendu** : URP (Universal Render Pipeline)
- **Contrôle de version** : Git + GitLab/GitHub
- **Gestion projet** : Trello / Notion / GitLab Issues
- **Modèles 3D sources** : Sketchfab, TurboSquid (licences libres)
- **Appareil cible** : Samsung Tab S7 FE (ARCore compatible)

---

## 📋 Critères de réussite (Definition of Done)

- Application fonctionnelle sur tablette (Android).
- Suivi temps réel de la pose humaine (tracking stable).
- Couches anatomiques activables/désactivables.
- Mini-jeu jouable et intégré.
- Mode “Carlos” opérationnel.
- Documentation complète + vidéo de présentation.

---

## ⚠️ Risques et mitigations

| Risque | Impact | Mitigation |
|--------|---------|------------|
| Pose estimation lente ou imprécise | Suivi erratique | Filtrage / fallback manuel (gommettes) |
| Modèles 3D trop lourds | FPS faible | LOD, simplification, textures allégées |
| Retargeting IK complexe | Délai sur tracking | Simplifier rig ou utiliser modèle humanoïde Unity |
| Occlusions (bras devant torse) | Perte tracking | Interpolation + message UX “Recalibrez” |
| Conflits Git | Intégration lente | Branches nommées et merges hebdomadaires |

---

## 📄 Livrables finaux

- **APK final** signé et testé.
- **Dépôt Git complet** (code + modèles + README).
- **Rapport technique** (architecture, choix technos, résultats, tests).
- **Vidéo de démonstration** (1–2 minutes).
- **Présentation orale / slides** pour soutenance.

---

## 📅 Exemple de timeline (6 semaines)

| Semaine | Phase principale | État |
|----------|------------------|------|
| 0 | Kickoff, setup, planning | 🟢 |
| 1–2 | Prototype (caméra, pose, modèle 3D, UI base) | 🟡 |
| 3 | Tracking + couches anatomiques | 🟡 |
| 4 | Mini-jeu + amélioration tracking | 🟡 |
| 5 | Mode “Carlos” + finitions | 🟡 |
| 6 | Tests finaux, build, doc, présentation | 🔵 |

---

## 🧠 Pour aller plus loin

- Ajouter des infos contextuelles sur chaque organe (labels interactifs).
- Intégrer reconnaissance vocale (“montre les muscles du bras”).
- Ajouter un mode **“vue médicale”** avec transparence ajustable.
- Support multi-utilisateur (prof + élève).

---

## 📚 Ressources utiles

- [Unity AR Foundation Docs](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@latest)
- [Google ARCore Developer Guide](https://developers.google.com/ar)
- [YOLO-Pose (Ultralytics)](https://docs.ultralytics.com/modes/predict/#pose)
- [MediaPipe BlazePose](https://developers.google.com/mediapipe/solutions/vision/pose)
- [Sketchfab Anatomical Models](https://sketchfab.com/tags/anatomy)

---

## 💬 Auteurs
Projet réalisé par :  
**[Noms de l’équipe]**  
Master / Licence [Nom du programme] – [Année universitaire]

---

