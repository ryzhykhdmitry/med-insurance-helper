# Create Azure OpenAI service
az cognitiveservices account create `
    --name medinsurance-openai `
    --resource-group med-insurance-rg `
    --location eastus `
    --kind OpenAI `
    --sku S0 `
    --yes

# Deploy embedding model (required for indexer)
az cognitiveservices account deployment create `
    --name medinsurance-openai `
    --resource-group med-insurance-rg `
    --deployment-name text-embedding-ada-002 `
    --model-name text-embedding-ada-002 `
    --model-version "2" `
    --model-format OpenAI `
    --sku-capacity 120 `
    --sku-name "Standard"

# Deploy chat model (required for chat API)
az cognitiveservices account deployment create `
    --name medinsurance-openai `
    --resource-group med-insurance-rg `
    --deployment-name gpt-35-turbo `
    --model-name gpt-35-turbo `
    --model-version "0613" `
    --model-format OpenAI `
    --sku-capacity 120 `
    --sku-name "Standard"