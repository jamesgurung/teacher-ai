# Organisation AI

Organisation AI is a free, open-source web application that empowers your entire organisation with access to advanced AI chat capabilities&mdash;without the burden of costly per-user licenses.

Bring your own OpenAI key and deploy effortlessly to Microsoft Azure.

![Screenshot of Organisation AI](examples/screenshot.png)

Originally known as *Teacher AI* for its focus on supporting staff in schools, this app has been upgraded to meet the needs of any organisation, from businesses to nonprofits.

### Features

* A clean, professional chat interface
* Access to the latest OpenAI language models
* Customisable prompt presets for different teams
* Web search capabilities
* Understanding of uploaded images
* Document search using vector stores
* Chat history
* Automated content moderation and manual review
* Configurable weekly spend limits
* Support for all screen sizes, including mobile
* Single sign-on (SSO) using Microsoft 365

### Setup

1. Create an OpenAI account and generate an API key.

2. Create a general purpose v2 storage account in Microsoft Azure, and within it create:
    * Tables: `conversations`, `spend`, and `review`
    * Blob containers: `conversations` and `config`

3. Within the `config` blob container, upload a blank file `keys.xml`. Generate a SAS URL for this file with read/write permissions and a distant expiry. This will be used to store the application's data protection keys so that auth cookies persist across app restarts.


4. Within the `config` blob container, create a file `users.csv` with the following format:

    ```csv
    Email,UserGroup
    test@example.com,staff
    ```

5. For each user group configured in `users.csv`, create a file `<usergroup>.json` with the following format:

    ```json
    {
      "allowUploads": true,
      "introMessage": "This service is for work-related queries only.",
      "presets": [
        {
          "id": "default",
          "title": null,
          "category": null,
          "introduction": null,
          "instructions": "You are a helpful AI assistant.",
          "model": "gpt-4.1",
          "temperature": 0.2,
          "reasoningEffort": null,
          "webSearchEnabled": false,
          "vectorStoreId": null
        }
      ],
      "showPresetDetails": true,
      "stopCommands": [
        "token": "[offtopic]",
        "message": "This is off-topic."
      ]
    }
    ```

    Our [examples](examples) folder contains some sample configuration files for a secondary school.

    The settings are:

    * `allowUploads` - whether users in this group can upload files
    * `introMessage` - a message to display to users on the homepage in Markdown format, e.g. you may include a link to your organisation's AI policy
    * `presets` - an array of preset prompt templates that will be available to users from the sidebar
        * `id` - the ID of the preset, which is a unique string without spaces; use the ID `default` to specify the default preset which loads at the start
        * `title` - the title of the preset, displayed in the sidebar
        * `category` - the category of the preset, displayed as a heading in the sidebar
        * `introduction` - the message to display when this preset is selected, in Markdown format (note that this is not sent to the language model)
        * `instructions` - the system instructions to send to the language model along with the user's message
        * `model` - the OpenAI model to use for this preset, as configured above
        * `temperature` (optional) - the temperature to use
        * `reasoningEffort` (optional) - the reasoning effort to use, where supported by the model
        * `webSearchEnabled` (optional) - whether web searches are enabled
        * `vectorStoreId` (optional) - the ID of the OpenAI vector store to use for this preset
    * `showPresetDetails` - whether to show the preset details, such as the system instructions and model name, to the user
    * `stopCommands` - an array of tokens that, when received from the language model, will stop the conversation and display a customised message; this needs to be used in conjunction with the model `instructions` above
 
6. Create an Azure app registration.
    * Name - `Organisation AI`
    * Redirect URI - `https://<app-website-domain>/signin-oidc`
    * Implicit grant - ID tokens
    * Supported account types - Accounts in this organizational directory only
    * API permissions - `Microsoft Graph - User.Read`
    * Token configuration - add an optional claim of type ID: `upn`

7. Create an Azure App Service web app.
    * Publish mode - Container
    * Operating system - Linux
    * Image source - Other container registries
    * Docker Hub access type - Public
    * Image and tag - `jamesgurung/organisation-ai:latest`
    * Startup command: (blank)

8. Configure the following environment variables for the web app:

    * `Organisation__Name` - the name of your organisation
    * `Organisation__AppWebsite` - the host name where this app will be hosted, e.g. `example.com`
    * `Organisation__UserMaxWeeklySpend` - the amount that each user is allowed to spend each week
    * `Organisation__Reviewers__0` - the email address of a user to give reviewer access, which allows them to review all users' AI conversations (subsequent reviewers can be configured by adding additional items with incrementing indices)
    * `Organisation__CountryCode` - your country code, e.g. `GB` for Great Britain (used for localised web search)
    * `Organisation__City` - your city name (used for localised web search)
    * `Organisation__Timezone` - your timezone, e.g. `Europe/London` (used for localised web search)
    * `Azure__ClientId` - the client ID of your Azure app registration
    * `Azure__TenantId` - your Azure tenant ID
    * `Azure__StorageAccountName` - the name of your Azure Storage account
    * `Azure__StorageAccountKey` - the key for your Azure Storage account
    * `Azure__DataProtectionBlobUri` - the SAS URL for the keys file you created earlier
    * `OpenAI__ApiKey` - the API key for your OpenAI account
    * `OpenAI__CostPer1KFileSearches` - the OpenAI credit cost per 1K file searches
    * `OpenAI__TitleSummarisationModel` - the model which will be used to summarise titles, e.g. `gpt-4.1-nano`
    * `OpenAI__Models__0__Name` - the name of an OpenAI model you would like to make available, e.g. `gpt-4.1` (subsequent models can be configured by adding additional items with incrementing indices)
    * `OpenAI__Models__0__CostPer1MInputTokens` - the model cost per 1M input tokens
    * `OpenAI__Models__0__CostPer1MCachedInputTokens` - the model cost per 1M cached input tokens
    * `OpenAI__Models__0__CostPer1MOutputTokens` - the model cost per 1M output tokens
    * `OpenAI__Models__0__CostPer1KWebSearches` - the model cost per 1K web searches (if the model does not support web searches, omit this setting)

### Contributing

If you have a question or feature request, please open an issue.

To contribute improvements to this project, or to adapt the code for the specific needs of your organisation, you are welcome to fork the repository.

Pull requests are welcome; please open an issue first to discuss.