# OpenEdAI – AI-Powered Personalized Learning Platform

![License](https://img.shields.io/badge/license-Custom-blue)
![Platform](https://img.shields.io/badge/platform-Blazor%20WASM%20%7C%20.NET%209-blueviolet)
![Hosted On](https://img.shields.io/badge/Hosted%20on-AWS-orange)
![Auth](https://img.shields.io/badge/Auth-AWS%20Cognito-lightgrey)  



**Current Estimated Operating Costs**  
![AWS Cost](https://img.shields.io/badge/AWS%20Monthly~$20-blue)
![OpenAI Cost](https://img.shields.io/badge/OpenAI%20Monthly~$0.11-purple)
![Google API Cost](https://img.shields.io/badge/Google%20API~Free%20(Tier)-green)  


OpenEdAI is a full-stack educational platform that dynamically generates personalized learning paths from free, trusted online resources using AI.  
Built for accessibility and scalability, OpenEdAI enables learners to define their goals and receive customized course plans tailored to their educational background and learning needs.

Originally developed as a Software Engineering Capstone Project at WGU, the platform now enters long-term development with active deployment on AWS.  



## Table of Contents

* [Live Application](#live-application)
* [Key Features](#key-features)
* [Architecture Overview](#architecture-overview)
* [Planned Enhancements](#planned-enhancements)
* [Operating Cost Breakdown](#estimated-operating-costs)
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

## Estimated Operating Costs

OpenEdAI is actively hosted and incurs monthly infrastructure and API usage costs. These are covered by the developer during ongoing development.

| Category             | Service                          | Current Cost (USD) |
|----------------------|-----------------------------------|--------------------|
| **Hosting**          | AWS ECS (Fargate, containers)     | $9.44              |
|                      | AWS RDS (Aurora MySQL)            | $4.64              |
|                      | AWS Load Balancer (ALB)           | $4.32              |
|                      | AWS VPC, Networking               | $1.20              |
|                      | EC2 - Other                       | $0.61              |
| **AI Services**      | OpenAI API (GPT-4o Mini)          | $0.11              |
| **Search APIs**      | Google Custom Search (820 queries)| $0.00 (Free Tier)  |
|                      | YouTube Data API                  | $0.00 (Quota-based)|
|                      |                                   |                    |
| **Estimated Total**  | *(as of May 2025)*                | **~$20.32**        |

> 💡 These estimates reflect current low-traffic usage during active development. As public usage increases, so may costs (especially OpenAI and Google Search APIs which charge per request).

## Contributing

Currently in single-developer maintenance mode. Contributions are not currently accepted unless explicitly authorized by the repository owner.

If granted permission:

* Use a working branch: `feature/your-feature`
* Follow C# clean architecture best practices
* Use conventional commits:

  ```
  type(scope): description
  ```

## License

This project is licensed under a **Custom Personal Use License**.

The codebase may only be used for **personal, non-commercial, and educational purposes**.
**Redistribution, commercial use, or modification is strictly prohibited** without prior written consent from the author.

This software is **not open source** and does not grant any of the permissions typically associated with OSI-approved licenses.

See the [LICENSE](./LICENSE) file for the full terms.

© 2025 Robert Morgan – All rights reserved.
