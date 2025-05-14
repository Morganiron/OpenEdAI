# OpenEdAI – AI-Powered Personalized Learning Platform

OpenEdAI is a full-stack educational platform that dynamically generates personalized learning paths from free, trusted online resources using AI.  
Built for accessibility and scalability, OpenEdAI enables learners to define their goals and receive customized course plans tailored to their educational background and learning needs.

Originally developed as a Software Engineering Capstone Project at WGU, the platform now enters long-term development with active deployment on AWS.

## Table of Contents

* [Live Application](#live-application)
* [Key Features](#key-features)
* [Architecture Overview](#architecture-overview)
* [Planned Enhancements](#planned-enhancements)
* [Contributing](#contributing)
* [License](#license)

## Live Application

Frontend: [https://openedai.morganiron.com](https://openedai.morganiron.com)  
Authentication: Managed via AWS Cognito Hosted UI

## Key Features

* **AI-Generated Learning Plans** – OpenAI builds custom course structures based on topic, experience level, and user profile
* **Dynamic Resource Linking** – Relevant YouTube and article content is fetched, vetted, and assigned to each lesson
* **Progress Tracking** – Interactive dashboards visualize lesson and course completion
* **Student Profiles** – Users provide education level, preferred formats, and special considerations to shape course recommendations
* **Secure Auth** – Cognito-based JWT authentication with session refresh and auto-login
* **Asynchronous Processing** – Background task queue handles long-running link generation processes

## Architecture Overview

| Layer       		| Stack/Services                                                      |
| ----------- 		| ------------------------------------------------------------------- |
| Frontend    		| Blazor WebAssembly (.NET 9), Deployed to AWS S3 + CloudFront        |
| Backend     		| ASP.NET Core Web API (.NET 9), Dockerized + Deployed on ECS Fargate |
| Database    		| Amazon Aurora (MySQL 8.x)                                           |
| Auth        		| AWS Cognito (Hosted UI + OAuth2 Flow, JWT-based Authorization)      |
| Secrets     		| AWS Secrets Manager in production, .NET User Secrets in development |
| AI Services		| OpenAI API (GPT-4o Mini), Google Custom Search, YouTube Data API    |
| Infrastructure	| AWS: ECS, RDS, S3, CloudFront, ALB, ACM, Route 53                   |

## Planned Enhancements

* CI/CD Pipeline – GitHub Actions for ECS (API) and S3 (WASM)
* Quizzes + Assessments – Optional knowledge checks per lesson
* Achievements – Badges for completed courses and progress
* Usage Monitoring – CloudWatch metrics and autoscaling triggers
* Public Course Sharing – Share links to AI-generated courses

## Contributing

Currently in single-developer maintenance mode. Pull requests are welcome with prior discussion. To contribute:

* Fork the repository
* Use a working branch: feature/your-feature
* Follow C# clean architecture best practices and commit with:

```
type(scope): description
```

## License

This project is licensed under the [Creative Commons BY-NC-ND 4.0 License](https://creativecommons.org/licenses/by-nc-nd/4.0/).  
It is intended **solely for educational and demonstration purposes**.

**Commercial use, redistribution, or modification of this codebase is strictly prohibited without explicit written permission from the author.**

© Robert Morgan – Morganiron
