/* Finder identifies supported architectures from a Mach-O executable. */
#include <mach-o/dyld.h>
#include <stdint.h>
#include <stdio.h>
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

    char script[sizeof(executable) + sizeof(".sh")];
    snprintf(script, sizeof(script), "%s.sh", executable);
    execl("/bin/bash", "bash", script, (char *)NULL);
    perror("Start HNMO");
    return 1;
}
