# Spec Overview

## 1. Purpose

Symptom Checker (Educational) is a Windows Forms desktop application that allows users to select symptoms from a predefined checkbox list and receive scored suggestions of possible medical conditions from a local JSON dataset. It is designed exclusively for **educational and learning purposes** — to help students, developers, and curious learners understand how symptom-to-condition matching algorithms work.

## 2. Educational Scope

| In Scope | Explicitly Out of Scope |
|---|---|
| Demonstrate set-overlap, cosine, and Bayesian matching | Clinical diagnosis or triage decisions |
| Illustrate decision-rule scoring (Centor/McIsaac, PERC) | Any form of treatment recommendation that could be acted upon |
| Teach red-flag pattern recognition concepts | Replacement for professional medical consultation |
| Show internationalization patterns (EN/FR/AR + RTL) | Regulatory compliance as a medical device |
| Expose data-sync workflows (Wikidata SPARQL) | Free-text NLP or symptom extraction |

The application displays educational disclaimers at all times and must never be marketed, positioned, or relied upon as a medical diagnostic tool.

## 3. Target Users

| Persona | Description |
|---|---|
| CS / Health-Informatics Students | Learning about matching algorithms, Bayesian classifiers, and decision-support systems |
| Software Developers | Studying WinForms architecture, i18n with RTL, JSON data workflows, or desktop app patterns |
| Educators / Instructors | Using the app as a teaching aid in classroom or lab settings |
| Self-learners | Exploring how symptom checkers conceptually work without clinical reliance |

Users are **not** expected to be clinicians, and the application must never encourage clinical use.

## 4. Non-Goals (Explicit Exclusions)

| ID | Non-Goal |
|---|---|
| NG-1 | The application is **not** a medical device and must not be classified, certified, or marketed as one. |
| NG-2 | No free-text symptom input will be introduced; all input is checkbox-based from a curated list. |
| NG-3 | No cloud services, API keys, user accounts, or telemetry will be required. Wikidata sync is optional and key-free. |
| NG-4 | No patient data (PHI/PII) is collected, stored, or transmitted. |
| NG-5 | The application does not aim for clinical accuracy, sensitivity, or specificity guarantees. |
| NG-6 | No real-time monitoring, alerts, or integration with EHR/EMR systems. |
| NG-7 | The application will not evolve into a multi-user or server-based system within the current scope. |

## 5. Platform & Runtime

| Attribute | Value |
|---|---|
| Framework | .NET 8 SDK (Windows) |
| UI Toolkit | Windows Forms |
| Target OS | Windows 10+ (x64) |
| Offline-first | Yes — all core features work without network access |
| External dependency | NJsonSchema (schema validation only) |

## 6. Disclaimer Requirement

Every user-facing surface — main window, details dialogs, export files, triage banners — must include or reference the disclaimer:

> **This tool is for educational purposes only and is not a substitute for professional medical advice, diagnosis, or treatment.**

This statement must appear in all supported languages.
