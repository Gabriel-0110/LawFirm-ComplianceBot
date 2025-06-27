# Teams Compliance Bot - Copilot Instructions

<!-- Use this file to provide workspace-specific custom instructions to Copilot. For more details, visit https://code.visualstudio.com/docs/copilot/copilot-customization#_use-a-githubcopilotinstructionsmd-file -->

## Project Overview
This is a Microsoft Teams compliance bot for call recording built with .NET Core, designed to:
- Record Teams calls automatically for compliance purposes
- Store recordings securely in Azure Blob Storage
- Integrate with Microsoft Graph API for Teams functionality
- Follow Microsoft security and compliance best practices
- Be deployable on Azure VMs

## Key Technologies
- .NET 8.0 Web API
- Microsoft Bot Framework
- Microsoft Graph SDK
- Azure Blob Storage
- Azure Identity (Managed Identity preferred)
- Microsoft Teams SDK

## Architecture Guidelines
- Use dependency injection for all services
- Implement proper error handling and logging
- Follow Azure security best practices
- Use managed identity for Azure authentication
- Implement proper async/await patterns
- Include comprehensive configuration management

## Security Requirements
- Never hardcode credentials or secrets
- Use Azure Key Vault for sensitive data
- Implement proper RBAC permissions
- Follow least privilege principles
- Ensure data encryption in transit and at rest
- Implement proper audit logging

## Compliance Features
- Auto-start recording on call join
- Store metadata about recordings
- Implement retention policies
- Provide audit trails
- Support legal hold scenarios
- Ensure GDPR compliance where applicable

When generating code, always consider:
1. Microsoft Teams compliance requirements
2. Azure deployment scenarios
3. Security and authentication patterns
4. Error handling and resilience
5. Logging and monitoring
6. Configuration management
