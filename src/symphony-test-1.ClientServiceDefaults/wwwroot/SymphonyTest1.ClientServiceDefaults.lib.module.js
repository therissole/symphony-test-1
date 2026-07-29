export async function onRuntimeConfigLoaded(config) {
    try {
        // Resolve against <base href> so prefixed gateway paths load their own Aspire config.
        const configUrl = new URL('_blazor/_configuration', document.baseURI).href;
        const response = await fetch(configUrl);

        if (!response.ok) {
            return;
        }

        const serverConfig = await response.json();
        const environmentVariables = serverConfig?.webAssembly?.environment;

        if (!environmentVariables || Object.keys(environmentVariables).length === 0) {
            return;
        }

        config.environmentVariables ??= {};

        for (const [key, value] of Object.entries(environmentVariables)) {
            config.environmentVariables[key] = value;
        }
    } catch (error) {
        console.warn('Failed to load Aspire client configuration:', error);
    }
}
