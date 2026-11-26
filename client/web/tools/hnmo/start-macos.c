/* Finder identifies supported architectures from a Mach-O executable. */
#include <mach-o/dyld.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include <unistd.h>

int main(void)
{
    char executable[4096];
    uint32_t size = sizeof(executable);
    if (_NSGetExecutablePath(executable, &size) != 0)
    {
        fputs("HNMO launcher path is too long.\n", stderr);
        return 1;
    }

    /* The script lives in Contents/Resources: a script in Contents/MacOS counts as code and cannot be sealed. */
    char *slash = strrchr(executable, '/');
    if (slash == NULL)
    {
        fputs("HNMO launcher path is not absolute.\n", stderr);
        return 1;
    }

    *slash = '\0'; /* .../Contents/MacOS */
    char script[sizeof(executable) + sizeof("/../Resources/start.sh")];
    snprintf(script, sizeof(script), "%s/../Resources/start.sh", executable);
    execl("/bin/bash", "bash", script, (char *)NULL);
    perror("Start HNMO");
    return 1;
}
