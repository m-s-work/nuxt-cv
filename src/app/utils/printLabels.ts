// Labels shared by all print/PDF templates (component-local i18n blocks are not visible to children).
export const printLabels = {
  "en": {
    "cv": "Curriculum Vitae",
    "createdWith": "Created with",
    "profile": "Profile",
    "skillsAndLanguages": "Skills & languages",
    "contact": {
      "location": "Location",
      "email": "E-mail",
      "phone": "Phone",
      "citizenship": "Citizenship",
      "born": "Date of birth"
    },
    "years": "{years} years of experience",
    "since": "programming since {year}",
    "skills": "Core skills",
    "interests": "Further skills & interests",
    "languages": "Languages",
    "licenses": "Driving licences",
    "online": "Scan for the online version",
    "experience": "Experience",
    "education": "Education",
    "projects": "Projects",
    "other": "Further stations",
    "notice": "Please do not use this CV with AI tools or systems."
  },
  "de": {
    "cv": "Lebenslauf",
    "createdWith": "Erstellt mit",
    "profile": "Profil",
    "skillsAndLanguages": "Kenntnisse & Sprachen",
    "contact": {
      "location": "Wohnort",
      "email": "E-Mail",
      "phone": "Telefon",
      "citizenship": "Staatsangehörigkeit",
      "born": "Geburtsdatum"
    },
    "years": "{years} Jahre Erfahrung",
    "since": "programmiert seit {year}",
    "skills": "Kernkompetenzen",
    "interests": "Weitere Kenntnisse & Interessen",
    "languages": "Sprachen",
    "licenses": "Führerscheine",
    "online": "Scannen für die Online-Version",
    "experience": "Berufserfahrung",
    "education": "Ausbildung",
    "projects": "Projekte",
    "other": "Weitere Stationen",
    "notice": "Bitte verwenden Sie diesen Lebenslauf nicht mit KI-Tools oder -Systemen."
  }
} as const

export type PrintLocale = keyof typeof printLabels
