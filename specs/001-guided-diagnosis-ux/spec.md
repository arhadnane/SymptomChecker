# Feature Specification: Guided Diagnosis Assistant (Patient + Professional Modes)

**Feature Branch**: `001-guided-diagnosis-ux`
**Created**: 2026-05-23
**Status**: In Progress — shell host, Patient wizard, Professional shell, persisted collapsible sections, patient-facing string audit, and contrast verification completed on 2026-05-23; full manual smoke remains pending.
**Input**: User description: "Rendre l'application facile à utiliser, capable de jouer le rôle d'un docteur pour orienter un diagnostic et proposer des médicaments, tout en restant un outil éducationnel avec disclaimer permanent. Utilisable comme conseil ou par les médecins. Améliorer le design, la responsivité, l'ergonomie et l'UX."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Mode Patient guidé en 4 étapes (Priority: P1)

Un utilisateur non technique (patient, étudiant, parent inquiet) ouvre l'application et est accueilli par un sélecteur de mode (« Mode Patient » vs « Mode Professionnel »). En choisissant le Mode Patient, il est guidé pas à pas dans un assistant en quatre étapes : (1) choix d'une zone corporelle / catégorie via de grandes cartes ; (2) sélection des symptômes liés à la zone choisie ; (3) informations de santé optionnelles et questions de sécurité formulées simplement ; (4) écran de résultats lisible montrant pour chaque condition possible : nom, niveau de confiance (faible/modéré/élevé), traitements maison éducatifs, exemples de médicaments OTC (en vente libre), conseils, et un encart rouge persistant rappelant « Ceci n'est pas un diagnostic — consultez un professionnel de santé ».

**Why this priority**: C'est la valeur principale demandée — rendre l'application accessible à un public non clinique tout en gardant le cadre éducatif. Sans ce mode, l'app reste réservée aux utilisateurs avertis. La présentation lisible des médicaments répond explicitement à la demande.

**Independent Test**: Peut être validé indépendamment en lançant l'application, en sélectionnant Mode Patient, en parcourant les 4 étapes pour un cas simple (ex : fièvre + maux de tête + courbatures), et en vérifiant que les résultats affichent au moins une condition probable avec traitements/médicaments/disclaimer renforcé, le tout sans avoir touché à un seul paramètre technique (modèle, seuil, top-K, poids, température).

**Acceptance Scenarios**:

1. **Given** l'utilisateur lance l'app pour la première fois, **When** l'écran d'accueil s'affiche, **Then** un sélecteur de mode propose « Patient (guidé) » et « Professionnel (avancé) » avec une brève description et le disclaimer éducatif visible.
2. **Given** l'utilisateur a sélectionné « Patient », **When** il avance étape par étape sans modifier les options par défaut, **Then** il atteint l'écran de résultats sans rencontrer aucun terme technique (Jaccard, Naive Bayes, seuil, Top-K, température, poids de catégorie) et chaque écran affiche un disclaimer en bas.
3. **Given** l'utilisateur arrive à l'écran de résultats avec au moins une correspondance, **When** il clique sur une condition, **Then** il voit clairement les sections « Traitements maison », « Médicaments en vente libre (exemples)», « Quand consulter », et un encart rouge « Toujours consulter un professionnel » non rejetable.
4. **Given** l'utilisateur n'a sélectionné aucun symptôme à l'étape 2, **When** il clique sur « Suivant », **Then** un message explique qu'au moins un symptôme est requis (pas une erreur technique).
5. **Given** un drapeau rouge est détecté (ex : SpO₂ < 92 % ou douleur thoracique avec essoufflement), **When** l'écran de résultats s'affiche, **Then** une bannière d'urgence rouge en haut indique « Symptômes urgents — appelez immédiatement les services d'urgence » dans la langue choisie.

---

### User Story 2 - Mode Professionnel densifié et organisé (Priority: P2)

Un médecin, étudiant en médecine ou enseignant utilise le Mode Professionnel pour accéder à toutes les options actuelles (3 modèles de matching, seuils, poids par catégorie, température NB, vitals, Centor/McIsaac, PERC, AI/Ollama, analyse d'image, etc.) mais avec une mise en page ré-organisée par sections claires (« Sélection des symptômes », « Constantes vitales », « Règles décisionnelles », « Réglages du modèle », « Résultats », « Modules IA ») au lieu d'une seule grande barre d'outils dense. Les sections de droite utilisent des panneaux repliables persistés dans `CollapsedSections`; la section « Sélection des symptômes » réutilise l'état du panneau gauche déjà existant.

**Why this priority**: Indispensable pour ne pas perdre les utilisateurs existants ni le caractère « médecin » du produit, mais peut être livré après le Mode Patient car les capacités sont déjà présentes.

**Independent Test**: Lancer l'app, basculer en Mode Professionnel, vérifier que tous les contrôles existants restent accessibles, fermer/rouvrir l'app et vérifier que le mode et l'état des sections repliées sont restaurés.

**Acceptance Scenarios**:

1. **Given** l'utilisateur sélectionne Mode Professionnel, **When** l'interface principale s'affiche, **Then** les contrôles sont regroupés en sections nommées et repliables (au moins 6 sections distinctes).
2. **Given** un médecin replie « Modules IA » et « Règles décisionnelles », **When** il relance l'application, **Then** ces sections sont à nouveau repliées par défaut.
3. **Given** l'utilisateur est en Mode Professionnel, **When** il clique sur « Passer en Mode Patient », **Then** la transition est immédiate, la sélection des symptômes courants est préservée et le disclaimer reste visible.

---

### User Story 3 - Refonte du panneau de résultats avec sections médicaments (Priority: P1)

Indépendamment du mode actif, chaque résultat retourné affiche les informations existantes du dataset (`Treatments`, `Medications`, `CareAdvice` et leurs variantes localisées `_Fr` / `_Ar`) directement dans une carte lisible — sans devoir double-cliquer — avec : un titre lisible, un badge de confiance, le nombre de symptômes correspondants, et trois sections nommées (« Soins éducatifs », « Exemples de médicaments OTC », « Quand consulter »). Un encart rouge persistant intitulé « Important » rappelle le caractère éducatif et le besoin de consulter un professionnel, en EN/FR/AR avec support RTL.

**Why this priority**: Concrétise la demande « propose des médicaments » en exploitant les données déjà présentes (98 conditions, ~90 % avec champs `Medications` localisés). Bénéficie immédiatement aux deux modes.

**Independent Test**: Sélectionner manuellement « Fever » + « Cough » + « Fatigue », cliquer sur Check, observer que la carte « Flu » montre directement Acetaminophen, Ibuprofen, conseils, et un encart rouge — sans interaction supplémentaire.

**Acceptance Scenarios**:

1. **Given** au moins un résultat est retourné, **When** la liste de résultats est affichée, **Then** chaque entrée montre les sections traitements/médicaments/conseils directement dans la carte (pas seulement dans le dialogue de détails).
2. **Given** la langue active est `fr`, **When** la carte est affichée, **Then** les contenus `Treatments_Fr`, `Medications_Fr`, `CareAdvice_Fr` sont utilisés ; à défaut, les valeurs anglaises sont affichées en repli sans erreur.
3. **Given** la langue active est `ar`, **When** la carte est affichée, **Then** la direction RTL est appliquée à la carte et le disclaimer reste lisible.
4. **Given** une condition n'a pas de médicament listé, **When** sa carte est affichée, **Then** la section « Médicaments » indique « Aucun médicament en vente libre listé — consultez un professionnel » au lieu d'être vide.

---

### User Story 4 - Disclaimer renforcé et drapeaux rouges très visibles (Priority: P1)

Le disclaimer éducatif n'est jamais caché. Il apparaît : sur l'écran d'accueil (sélecteur de mode), sur chaque étape du Mode Patient, en bas permanent du Mode Professionnel, dans chaque carte de résultat, dans le dialogue de détails et en première ligne de tous les exports (CSV/MD/HTML). Les drapeaux rouges (SpO₂ bas, hypotension, tachycardie sévère, fièvre très élevée, PERC positif avec douleur thoracique) déclenchent une bannière rouge non rejetable en haut de l'écran de résultats avec un bouton « Appeler les services d'urgence ? » qui ouvre un dialogue d'information localisé (sans composer de numéro).

**Why this priority**: Exigence constitutionnelle (Principe I — Educational Safety First). Doit être livré dans la même itération que les Stories 1 et 3.

**Independent Test**: Saisir SpO₂ = 88 % avec « Shortness of Breath » sélectionné, lancer une vérification, vérifier qu'une bannière rouge non rejetable est affichée et que le dialogue d'urgence se localise correctement en EN/FR/AR.

**Acceptance Scenarios**:

1. **Given** un drapeau rouge est détecté, **When** les résultats s'affichent, **Then** la bannière rouge apparaît avec un texte localisé et un bouton « Quand appeler les urgences » non destructif.
2. **Given** l'utilisateur exporte les résultats en CSV/MD/HTML, **When** le fichier est ouvert, **Then** la première ligne (ou en-tête) contient le disclaimer éducatif intégral.
3. **Given** la fenêtre principale est redimensionnée à la taille minimale, **When** l'utilisateur regarde l'écran, **Then** le disclaimer reste visible et non tronqué.

---

### User Story 5 - Responsivité, lisibilité et thème améliorés (Priority: P2)

L'utilisateur peut redimensionner la fenêtre entre 800×600 et plein écran sans contrôles tronqués ni qui se chevauchent. Le mode sombre est entièrement supporté (incluant les nouvelles cartes de résultats et le wizard Patient). Le scaling DPI fonctionne sur des écrans 1080p et 4K. Les contrôles tactiles minimum atteignent 32 px de hauteur en Mode Patient.

**Why this priority**: Améliore le quotidien pour les deux modes, mais peut suivre la livraison initiale du Mode Patient.

**Independent Test**: Redimensionner la fenêtre à 800×600 et à 1920×1200, basculer entre clair et sombre, vérifier aucune coupure visuelle ; tester sur un écran à 150 % DPI.

**Acceptance Scenarios**:

1. **Given** la fenêtre est à 800×600, **When** l'app est en Mode Patient, **Then** tous les contrôles d'une étape sont visibles sans scroll horizontal.
2. **Given** le mode sombre est actif, **When** une carte de résultats est rendue, **Then** le contraste atteint au moins WCAG AA pour le texte (ratio ≥ 4.5 :1).
3. **Given** l'écran est à 150 % DPI, **When** l'app démarre, **Then** aucun texte de label, bouton ou disclaimer n'est tronqué ou superposé.

---

### Edge Cases

- Aucun symptôme sélectionné en Mode Patient → message clair en langue courante, pas d'erreur.
- Tous les filtres rendent la liste vide → suggestion d'élargir la sélection.
- `conditions.json` corrompu ou absent → l'écran d'accueil indique l'erreur et propose Help/Logs sans planter.
- Une condition a `Medications` vide et `Medications_Fr` rempli → en mode FR on affiche FR ; en EN on affiche le repli « Aucun médicament listé » (pas une chaîne FR pour un utilisateur EN).
- L'utilisateur bascule de Mode Patient à Pro au milieu d'une session → les symptômes sélectionnés sont préservés et visibles dans le panneau Pro.
- Localisation incomplète d'un nouveau label → repli vers EN, journalisation via `TranslationService.SaveMissingReport`.
- RTL (`ar`) + Mode Patient → ordre des cartes, sens de lecture du wizard et alignement du disclaimer restent corrects.
- Drapeau rouge déclenché alors qu'aucun résultat n'est trouvé → la bannière s'affiche quand même.

## Safety, Privacy & Data Impact *(mandatory)*

- **Educational Boundary**: Le mot « diagnostic » n'apparaît jamais sans le qualificatif « éducatif » ou sans le disclaimer associé. Les noms d'écrans utilisent « Orientation éducative », « Conditions possibles », « Suggestions éducatives ». Aucune posologie précise n'est affichée ; uniquement des classes ou exemples génériques (`Acetaminophen pour fièvre/douleur`). L'encart rouge « Important » est non rejetable sur les cartes de résultats. Les drapeaux rouges utilisent un libellé d'orientation (« Consultez immédiatement un médecin ») et non un diagnostic.

- **Data Handling**: Aucune nouvelle donnée patient persistée ni transmise. Le mode actif (Patient/Pro), l'état replié/déplié des sections, et la dernière étape atteinte sont ajoutés à `data/settings.json` (préférences UI uniquement, déjà non sensibles). Aucun appel réseau supplémentaire ; Wikidata et Ollama restent optionnels et masqués en Mode Patient.

- **Schema & Compatibility**: Aucun changement à `data/conditions.json`, `data/categories.json`, `data/synonyms.json`. Ajout de nouvelles clés UI dans `data/translations.json` (additif, repli EN garanti). Évolution de `AppSettings` (additif uniquement : `UiMode`, `CollapsedSections`, `PatientWizardLastStep`) ; les anciens fichiers sans ces champs continuent de fonctionner. Pas de nouveau fichier de schéma requis pour cette itération.

- **Localization & Accessibility**: Nouvelles clés de traduction effectivement livrées : `Mode_*`, `Patient_*`, `Pro_Section_*`, `Card_*`, `Confidence_*`. Toutes fournies en EN/FR/AR. Un audit automatisé empêche les termes trop techniques dans les chaînes `Patient_*`. Wizard Patient doit respecter l'ordre RTL en `ar`. Cibles tactiles ≥ 32 px en Mode Patient. Tab order défini pour toutes les nouvelles cartes. Performance : démarrage < 2 s, transition entre étapes < 200 ms, rendu d'une carte de résultats < 50 ms.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Au premier lancement, l'application MUST afficher un écran d'accueil proposant explicitement le choix entre Mode Patient (guidé) et Mode Professionnel (avancé), avec le disclaimer éducatif visible.
- **FR-002**: L'application MUST mémoriser le mode choisi dans `data/settings.json` et le restaurer aux lancements suivants ; un bouton « Changer de mode » MUST rester disponible à tout moment.
- **FR-003**: Le Mode Patient MUST présenter un assistant en exactement 4 étapes (Catégorie/Zone, Symptômes courants, Vitals & drapeaux rouges optionnels, Résultats).
- **FR-004**: Le Mode Patient MUST utiliser des libellés non techniques et ne MUST PAS exposer : choix du modèle, seuil %, top-K, poids par catégorie, température NB, paramètres d'export avancés, modules IA bruts.
- **FR-005**: Chaque étape du Mode Patient MUST afficher en bas un disclaimer éducatif lisible et non rejetable.
- **FR-006**: Le Mode Professionnel MUST regrouper les contrôles existants en au moins 6 sections nommées et chacune MUST être repliable, avec persistance de l'état replié dans `data/settings.json`.
- **FR-007**: La liste des résultats MUST afficher pour chaque condition une carte contenant : nom localisé, badge de confiance (Faible / Modéré / Élevé basé sur le score), nombre de symptômes correspondants, section « Soins éducatifs » (Treatments), section « Exemples de médicaments OTC » (Medications), section « Quand consulter » (CareAdvice), et un encart « Important » non rejetable.
- **FR-008**: Si un champ localisé (`*_Fr` / `*_Ar`) est manquant pour la langue active, le système MUST utiliser le champ anglais comme repli, sans erreur ni champ vide.
- **FR-009**: Si une condition n'a aucun médicament listé, le système MUST afficher le message localisé « Aucun médicament en vente libre listé — consultez un professionnel » à la place d'une section vide.
- **FR-010**: Le système MUST détecter les drapeaux rouges suivants et afficher une bannière rouge non rejetable en haut des résultats : SpO₂ < 92 %, SBP < 90, SBP ≥ 180 ou DBP ≥ 120, HR ≥ 120, RR ≥ 30, Temp ≥ 40 °C, PERC positif avec douleur thoracique ou essoufflement.
- **FR-011**: La bannière de drapeaux rouges MUST inclure un bouton « Quand appeler les urgences » qui ouvre un dialogue informatif localisé, sans composer aucun numéro et sans ouvrir d'application externe.
- **FR-012**: Tous les exports (CSV, Markdown, HTML) MUST contenir le disclaimer éducatif intégral en première ligne ou en en-tête visible.
- **FR-013**: Le système MUST supporter le redimensionnement de la fenêtre entre 800×600 et plein écran sans contrôles tronqués ou superposés.
- **FR-014**: Le système MUST supporter le DPI scaling à 100 %, 125 %, 150 % et 200 % sans tronquer le texte des nouveaux contrôles.
- **FR-015**: Toutes les nouvelles chaînes utilisateur MUST être disponibles en EN, FR et AR via `data/translations.json` ; en `ar`, les nouveaux conteneurs MUST respecter la disposition RTL.
- **FR-016**: La transition entre Mode Patient et Mode Professionnel MUST préserver la sélection courante des symptômes et des vitals.
- **FR-017**: Le système MUST exposer un raccourci clavier (par défaut `Ctrl+M`) pour basculer entre Mode Patient et Mode Professionnel, et `Échap` pour revenir à l'étape précédente du wizard.
- **FR-018**: Le système MUST appliquer la cible de contraste WCAG AA (≥ 4.5 :1) aux nouvelles cartes en mode clair et sombre.
- **FR-019**: La couverture xUnit MUST inclure : conversion score → badge de confiance, détection des drapeaux rouges (au moins 6 cas), repli localisation des champs Treatments/Medications/CareAdvice, persistance des nouvelles préférences UI.
- **FR-020**: Le wizard Patient MUST permettre un parcours minimal jusqu'aux résultats sans branchement supplémentaire, avec au plus trois actions de navigation avant après les entrées requises (Étape 1 → `Suivant`, Étape 2 → `Suivant`, Étape 3 → `Passer` ou `Suivant`). Aucun écran technique ou modal intermédiaire ne peut s'interposer.

### Key Entities *(include if feature involves data)*

- **UiMode**: enum logique persisté (`Patient` | `Professional`). Stocké dans `AppSettings.UiMode`. Pas de PHI.
- **PatientWizardState**: état transitoire de l'assistant (étape courante, catégorie choisie, sélection en cours). Non persisté entre sessions, sauf la dernière étape atteinte (`PatientWizardLastStep`). Dans l'implémentation actuelle, cet état est synchronisé entre `PatientShell` et `MainForm`.
- **ConditionResultCard**: vue agrégée d'un `ConditionMatch` + des champs `Treatments/Medications/CareAdvice` localisés + niveau de confiance dérivé.
- **RedFlag**: structure logique (`Code`, `Severity`, `LocalizedMessage`, `Trigger`) produite par un `TriageService` étendu à partir des vitals + symptômes + résultat PERC.
- **CollapsedSections**: dictionnaire `string -> bool` persisté dans `AppSettings.CollapsedSections` pour mémoriser l'état des panneaux du Mode Professionnel (`pro.model`, `pro.vitals`, `pro.rules`, `pro.results`, `pro.ai`). `pro.symptoms` reste aligné avec l'état replié du panneau gauche existant.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Un nouvel utilisateur peut compléter le parcours Patient (de l'écran d'accueil aux résultats) en moins de 90 secondes sans aide externe.
- **SC-002**: 100 % des conditions du dataset affichent leur section « Soins éducatifs », « Médicaments » et « Quand consulter » directement dans la carte (sans avoir à double-cliquer) — vérifiable par énumération de `data/conditions.json`.
- **SC-003**: 100 % des écrans, dialogues et exports affichent le disclaimer éducatif (vérifiable par revue manuelle des écrans + grep des templates d'export).
- **SC-004**: Au moins 6 conditions de drapeaux rouges (SpO₂, SBP bas, SBP/DBP haut, HR, RR, Temp) sont couvertes par des tests xUnit passants.
- **SC-005**: Le Mode Patient ne montre aucun des termes techniques suivants : « Jaccard », « Cosine », « Naive Bayes », « Threshold », « Top-K », « Temperature », « Category Weights » (vérifiable par revue des chaînes EN/FR/AR utilisées par l'assistant).
- **SC-006**: La fenêtre supporte un redimensionnement de 800×600 jusqu'à plein écran sans contrôles tronqués (vérifié sur deux résolutions, deux niveaux de DPI).
- **SC-007**: Le contraste mesuré sur les nouveaux libellés atteint au moins 4.5 :1 en mode clair et en mode sombre.
- **SC-008**: La couverture xUnit ajoute au moins 8 nouveaux tests passants liés aux histoires US1, US3 et US4.

## Assumptions

- Les utilisateurs en Mode Patient ne sont pas attendus comme cliniciens ; l'app NE doit JAMAIS prétendre poser un diagnostic.
- Le dataset existant (`data/conditions.json`, 98 conditions) est suffisant pour cette itération ; l'enrichissement (plus de médicaments, classes thérapeutiques) est explicitement hors scope et sera traité dans une feature future.
- La cible reste Windows 10+ / .NET 8 WinForms ; pas de portage web ou mobile dans cette itération.
- Les drapeaux rouges éducatifs (`TriageService`) restent éducatifs : aucune information cliniquement validée n'est promise, et tous les seuils gardent leur libellé « éducatif ».
- Les utilisateurs en Mode Professionnel conservent l'accès complet aux modules existants (Ollama, image, sang) ; ces modules sont masqués du Mode Patient mais ne sont pas supprimés.
- L'application reste mono-utilisateur, locale et hors-ligne par défaut (Wikidata, Ollama, image AI restent optionnels et user-initiated, conformément au Principe II de la constitution).
