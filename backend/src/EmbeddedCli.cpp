#include "EmbeddedCli.h"

// The command-line launcher is included as it is, from the file named by GWL_CLI_PATH. It goes in a data section, which the assembler
// writes for both Windows and Linux.
__asm__(
    ".data\n"
    ".balign 16\n"
    ".globl gwl_cli_begin\n"
    "gwl_cli_begin:\n"
    ".incbin \"" GWL_CLI_PATH "\"\n"
    ".globl gwl_cli_end\n"
    "gwl_cli_end:\n"
    ".text\n");

extern "C" const unsigned char gwl_cli_begin[];
extern "C" const unsigned char gwl_cli_end[];

namespace gwl {

std::pair<const unsigned char*, std::size_t> embeddedCli() {
    return {gwl_cli_begin, static_cast<std::size_t>(gwl_cli_end - gwl_cli_begin)};
}

}  // namespace gwl
