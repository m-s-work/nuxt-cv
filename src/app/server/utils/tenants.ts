export interface TenantConfig {
  id: string
  name: string
  domain?: string
  experiences: Experience[]
  studies: Study[]
  projects: Project[]
}

export interface Experience {
  id: number
  company: string
  position: string
  period: string
  description: string
  technologies: string[]
}

export interface Study {
  id: number
  institution: string
  degree: string
  period: string
  focus: string
}

export interface Project {
  id: number
  name: string
  type: string
  period: string
  description: string
  technologies: string[]
  images?: string[]
  screenshots?: string[]
  logo?: string
}

// Multi-tenant configuration
export const tenants: Record<string, TenantConfig> = {
  default: {
    id: 'default',
    name: 'Default CV',
    experiences: [
      {
        id: 1,
        company: 'Tech Company Inc.',
        position: 'Senior Software Architect',
        period: '2020 - Present',
        description: 'Leading architecture design and implementation for cloud-native applications',
        technologies: ['Nuxt', 'Vue.js', 'Node.js', 'Docker', 'Kubernetes']
      },
      {
        id: 2,
        company: 'Software Solutions Ltd.',
        position: 'Full Stack Developer',
        period: '2017 - 2020',
        description: 'Developed enterprise web applications and microservices',
        technologies: ['Vue.js', 'Express', 'PostgreSQL', 'Redis']
      }
    ],
    studies: [
      {
        id: 1,
        institution: 'Technical University',
        degree: 'Master of Science in Computer Science',
        period: '2015 - 2017',
        focus: 'Software Engineering & Distributed Systems'
      },
      {
        id: 2,
        institution: 'University of Technology',
        degree: 'Bachelor of Science in Computer Science',
        period: '2012 - 2015',
        focus: 'Computer Science Fundamentals'
      }
    ],
    projects: [
      {
        id: 1,
        name: 'E-Commerce Platform',
        type: 'Web Application',
        period: '2022 - 2023',
        description: 'Built a scalable e-commerce platform with microservices architecture, supporting millions of transactions per day',
        technologies: ['Vue.js', 'Node.js', 'MongoDB', 'Redis', 'Docker'],
        screenshots: ['/images/projects/ecommerce-1.png', '/images/projects/ecommerce-2.png'],
        logo: '/images/projects/ecommerce-logo.png'
      },
      {
        id: 2,
        name: 'Mobile Banking App',
        type: 'Mobile Application',
        period: '2021 - 2022',
        description: 'Developed a secure mobile banking application with biometric authentication and real-time transaction monitoring',
        technologies: ['React Native', 'TypeScript', 'GraphQL', 'PostgreSQL'],
        images: ['/images/projects/banking-app.png']
      },
      {
        id: 3,
        name: 'Cloud Infrastructure',
        type: 'DevOps Project',
        period: '2020 - 2021',
        description: 'Designed and implemented cloud infrastructure for enterprise applications using Infrastructure as Code',
        technologies: ['Kubernetes', 'Terraform', 'AWS', 'Jenkins', 'Prometheus'],
        logo: '/images/projects/cloud-logo.png'
      }
    ]
  }
}

export function getTenant(tenantId?: string): TenantConfig {
  return tenants[tenantId || 'default'] || tenants.default
}
