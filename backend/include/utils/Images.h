// Image helpers the launcher uses for icons: inflating zlib data, reading PNG and icon frames, scaling, and writing PNG.

#pragma once

#include <cstddef>
#include <cstdint>
#include <optional>

#include "utils/Strings.h"

namespace gwl {

// The raw RGBA pixels of an image, top row first.
struct Rgba {
    std::uint32_t width = 0;
    std::uint32_t height = 0;
    Bytes pixels;
};

// Inflates zlib data, as a PNG's compressed chunks are. Throws std::runtime_error when the data is corrupt.
Bytes zlibInflate(const Bytes& input);

// Inflates gzip data, as the gzip tool writes it. Throws std::runtime_error when the data is corrupt.
Bytes gzipInflate(const Bytes& input);

// A PNG file's image, for 8-bit grey, grey with alpha, RGB, RGBA and palette images without interlacing. Nothing for others.
std::optional<Rgba> decodePng(const Bytes& png);

// The largest frame of an ICO file, as RGBA. Frames are PNG or 32- or 24-bit BMP. Nothing when no frame can be read.
std::optional<Rgba> decodeLargestIcon(const Bytes& ico);

// The image scaled to fit within size by size, keeping its aspect ratio. Smaller images are scaled up.
Rgba scaleToFit(const Rgba& image, std::uint32_t size);

// The image as a PNG file, with its pixels stored without compression.
Bytes encodePng(const Rgba& image);

}  // namespace gwl
