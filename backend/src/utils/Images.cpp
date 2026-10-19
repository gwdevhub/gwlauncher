#include "utils/Images.h"

#include <algorithm>
#include <array>
#include <cstdlib>
#include <iterator>
#include <stdexcept>
#include <string>
#include <vector>

#include "utils/Bytes.h"

namespace gwl {

namespace {

const std::uint8_t kPngSignature[8] = {0x89, 'P', 'N', 'G', 0x0D, 0x0A, 0x1A, 0x0A};

bool startsWithPngSignature(const Bytes& data) {
    return data.size() >= 8 && std::equal(std::begin(kPngSignature), std::end(kPngSignature), data.begin());
}

std::uint32_t readU32BE(const Bytes& data, std::size_t at) {
    return static_cast<std::uint32_t>(data[at]) << 24 | static_cast<std::uint32_t>(data[at + 1]) << 16 |
           static_cast<std::uint32_t>(data[at + 2]) << 8 | static_cast<std::uint32_t>(data[at + 3]);
}

void appendU32BE(Bytes& data, std::uint32_t value) {
    for (int shift = 24; shift >= 0; shift -= 8) {
        data.push_back(static_cast<std::uint8_t>(value >> shift));
    }
}

std::uint32_t crc32(const std::string& type, const Bytes& body) {
    static std::array<std::uint32_t, 256> table{};
    static const bool built = [] {
        for (std::uint32_t n = 0; n < 256; ++n) {
            std::uint32_t c = n;
            for (int k = 0; k < 8; ++k) {
                c = (c & 1) ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }
            table[n] = c;
        }
        return true;
    }();
    (void)built;
    std::uint32_t crc = 0xFFFFFFFFu;
    auto feed = [&crc](std::uint8_t byte) { crc = table[(crc ^ byte) & 0xFF] ^ (crc >> 8); };
    for (char c : type) {
        feed(static_cast<std::uint8_t>(c));
    }
    for (std::uint8_t byte : body) {
        feed(byte);
    }
    return crc ^ 0xFFFFFFFFu;
}

std::uint32_t adler32(const Bytes& data) {
    std::uint32_t a = 1;
    std::uint32_t b = 0;
    for (std::uint8_t byte : data) {
        a = (a + byte) % 65521;
        b = (b + a) % 65521;
    }
    return b << 16 | a;
}

void appendChunk(Bytes& out, const std::string& type, const Bytes& body) {
    appendU32BE(out, static_cast<std::uint32_t>(body.size()));
    out.insert(out.end(), type.begin(), type.end());
    out.insert(out.end(), body.begin(), body.end());
    appendU32BE(out, crc32(type, body));
}

// The byte the PNG filter predicts from: left, above, or above-left, as the PNG specification defines the Paeth predictor.
std::uint8_t paeth(int a, int b, int c) {
    const int p = a + b - c;
    const int pa = std::abs(p - a);
    const int pb = std::abs(p - b);
    const int pc = std::abs(p - c);
    if (pa <= pb && pa <= pc) {
        return static_cast<std::uint8_t>(a);
    }
    return static_cast<std::uint8_t>(pb <= pc ? b : c);
}

}  // namespace

std::optional<Rgba> decodePng(const Bytes& png) {
    if (!startsWithPngSignature(png)) {
        return std::nullopt;
    }
    std::uint32_t width = 0;
    std::uint32_t height = 0;
    std::uint8_t colorType = 0;
    std::uint8_t bitDepth = 0;
    std::uint8_t interlace = 0;
    Bytes palette;
    Bytes compressed;
    std::size_t at = 8;
    while (at + 12 <= png.size()) {
        const std::uint32_t length = readU32BE(png, at);
        const std::string type(png.begin() + static_cast<std::ptrdiff_t>(at + 4), png.begin() + static_cast<std::ptrdiff_t>(at + 8));
        if (at + 12 + length > png.size()) {
            return std::nullopt;
        }
        const Bytes body(png.begin() + static_cast<std::ptrdiff_t>(at + 8), png.begin() + static_cast<std::ptrdiff_t>(at + 8 + length));
        if (type == "IHDR" && body.size() >= 13) {
            width = readU32BE(body, 0);
            height = readU32BE(body, 4);
            bitDepth = body[8];
            colorType = body[9];
            interlace = body[12];
        } else if (type == "PLTE") {
            palette = body;
        } else if (type == "IDAT") {
            compressed.insert(compressed.end(), body.begin(), body.end());
        } else if (type == "IEND") {
            break;
        }
        at += 12 + length;
    }

    // Only the layouts the launcher's icons use: 8-bit samples, without interlacing.
    if (width == 0 || height == 0 || bitDepth != 8 || interlace != 0) {
        return std::nullopt;
    }
    std::size_t channels = 0;
    switch (colorType) {
        case 0:
            channels = 1;
            break;
        case 2:
            channels = 3;
            break;
        case 3:
            channels = 1;
            break;
        case 4:
            channels = 2;
            break;
        case 6:
            channels = 4;
            break;
        default:
            return std::nullopt;
    }
    const std::size_t stride = static_cast<std::size_t>(width) * channels;
    Bytes raw;
    try {
        raw = zlibInflate(compressed);
    } catch (const std::runtime_error&) {
        return std::nullopt;
    }
    if (raw.size() < (stride + 1) * height) {
        return std::nullopt;
    }

    // Undo the per-row filters. Each row starts with its filter type; the bytes to the left count one pixel back.
    Bytes samples(stride * height, 0);
    for (std::size_t y = 0; y < height; ++y) {
        const std::uint8_t filter = raw[y * (stride + 1)];
        const std::size_t source = y * (stride + 1) + 1;
        std::uint8_t* row = samples.data() + y * stride;
        const std::uint8_t* above = y > 0 ? samples.data() + (y - 1) * stride : nullptr;
        for (std::size_t x = 0; x < stride; ++x) {
            const int left = x >= channels ? row[x - channels] : 0;
            const int up = above ? above[x] : 0;
            const int upLeft = (above && x >= channels) ? above[x - channels] : 0;
            int predicted = 0;
            switch (filter) {
                case 0:
                    predicted = 0;
                    break;
                case 1:
                    predicted = left;
                    break;
                case 2:
                    predicted = up;
                    break;
                case 3:
                    predicted = (left + up) / 2;
                    break;
                case 4:
                    predicted = paeth(left, up, upLeft);
                    break;
                default:
                    return std::nullopt;
            }
            row[x] = static_cast<std::uint8_t>(raw[source + x] + predicted);
        }
    }

    Rgba image;
    image.width = width;
    image.height = height;
    image.pixels.resize(static_cast<std::size_t>(width) * height * 4);
    for (std::size_t i = 0; i < static_cast<std::size_t>(width) * height; ++i) {
        const std::uint8_t* p = samples.data() + i * channels;
        std::uint8_t* out = image.pixels.data() + i * 4;
        switch (colorType) {
            case 0:
                out[0] = out[1] = out[2] = p[0];
                out[3] = 255;
                break;
            case 2:
                out[0] = p[0];
                out[1] = p[1];
                out[2] = p[2];
                out[3] = 255;
                break;
            case 3: {
                const std::size_t index = p[0];
                if (palette.size() < index * 3 + 3) {
                    return std::nullopt;
                }
                out[0] = palette[index * 3];
                out[1] = palette[index * 3 + 1];
                out[2] = palette[index * 3 + 2];
                out[3] = 255;
                break;
            }
            case 4:
                out[0] = out[1] = out[2] = p[0];
                out[3] = p[1];
                break;
            default:
                out[0] = p[0];
                out[1] = p[1];
                out[2] = p[2];
                out[3] = p[3];
                break;
        }
    }
    return image;
}

namespace {

// One frame of an ICO file, as RGBA. BMP frames hold a bottom-up image: 32-bit pixels carry their own alpha, and 24-bit ones use
// the AND mask that follows them.
std::optional<Rgba> decodeFrame(const Bytes& frame) {
    if (startsWithPngSignature(frame)) {
        return decodePng(frame);
    }
    if (frame.size() < 40) {
        return std::nullopt;
    }
    const std::uint32_t headerSize = readU32LE(frame, 0);
    const auto width = static_cast<std::int32_t>(readU32LE(frame, 4));
    // The height covers the colour image and the mask, so the colour image is half of it.
    const auto doubledHeight = static_cast<std::int32_t>(readU32LE(frame, 8));
    const std::uint16_t bitCount = readU16LE(frame, 14);
    if (headerSize < 40 || width <= 0 || doubledHeight <= 0 || frame.size() < headerSize) {
        return std::nullopt;
    }
    const auto height = doubledHeight / 2;
    const std::size_t w = static_cast<std::size_t>(width);
    const std::size_t h = static_cast<std::size_t>(height);
    const std::size_t maskStride = ((w + 31) / 32) * 4;
    Rgba image;
    image.width = static_cast<std::uint32_t>(w);
    image.height = static_cast<std::uint32_t>(h);
    image.pixels.assign(w * h * 4, 0);

    if (bitCount == 32) {
        const std::size_t stride = w * 4;
        if (frame.size() < headerSize + stride * h) {
            return std::nullopt;
        }
        for (std::size_t y = 0; y < h; ++y) {
            const std::size_t source = headerSize + (h - 1 - y) * stride;
            for (std::size_t x = 0; x < w; ++x) {
                std::uint8_t* out = image.pixels.data() + (y * w + x) * 4;
                out[0] = frame[source + x * 4 + 2];
                out[1] = frame[source + x * 4 + 1];
                out[2] = frame[source + x * 4];
                out[3] = frame[source + x * 4 + 3];
            }
        }
        return image;
    }
    if (bitCount == 24) {
        const std::size_t stride = (w * 3 + 3) / 4 * 4;
        const std::size_t maskAt = headerSize + stride * h;
        if (frame.size() < maskAt + maskStride * h) {
            return std::nullopt;
        }
        for (std::size_t y = 0; y < h; ++y) {
            const std::size_t source = headerSize + (h - 1 - y) * stride;
            const std::size_t mask = maskAt + (h - 1 - y) * maskStride;
            for (std::size_t x = 0; x < w; ++x) {
                std::uint8_t* out = image.pixels.data() + (y * w + x) * 4;
                out[0] = frame[source + x * 3 + 2];
                out[1] = frame[source + x * 3 + 1];
                out[2] = frame[source + x * 3];
                const bool transparent = (frame[mask + x / 8] >> (7 - x % 8)) & 1;
                out[3] = transparent ? 0 : 255;
            }
        }
        return image;
    }
    return std::nullopt;
}

}  // namespace

std::optional<Rgba> decodeLargestIcon(const Bytes& ico) {
    if (ico.size() < 6 || readU16LE(ico, 2) != 1) {
        return std::nullopt;
    }
    const std::size_t count = readU16LE(ico, 4);
    std::optional<std::size_t> best;
    std::size_t bestArea = 0;
    for (std::size_t i = 0; i < count; ++i) {
        const std::size_t at = 6 + i * 16;
        if (ico.size() < at + 16) {
            return std::nullopt;
        }
        // A width or height of 0 means 256.
        const std::size_t width = ico[at] == 0 ? 256 : ico[at];
        const std::size_t height = ico[at + 1] == 0 ? 256 : ico[at + 1];
        if (width * height > bestArea) {
            bestArea = width * height;
            best = i;
        }
    }
    if (!best) {
        return std::nullopt;
    }
    const std::size_t at = 6 + *best * 16;
    const std::size_t size = readU32LE(ico, at + 8);
    const std::size_t offset = readU32LE(ico, at + 12);
    if (offset > ico.size() || size > ico.size() - offset) {
        return std::nullopt;
    }
    return decodeFrame(Bytes(ico.begin() + static_cast<std::ptrdiff_t>(offset),
                             ico.begin() + static_cast<std::ptrdiff_t>(offset + size)));
}

Rgba scaleToFit(const Rgba& image, std::uint32_t size) {
    if (image.width == 0 || image.height == 0) {
        return image;
    }
    const double scale = std::min(static_cast<double>(size) / image.width, static_cast<double>(size) / image.height);
    Rgba out;
    out.width = std::max<std::uint32_t>(1, static_cast<std::uint32_t>(image.width * scale + 0.5));
    out.height = std::max<std::uint32_t>(1, static_cast<std::uint32_t>(image.height * scale + 0.5));
    out.pixels.assign(static_cast<std::size_t>(out.width) * out.height * 4, 0);
    for (std::uint32_t y = 0; y < out.height; ++y) {
        // The source rows this output row covers, averaged when the image shrinks.
        const std::uint32_t y0 = static_cast<std::uint32_t>(static_cast<std::uint64_t>(y) * image.height / out.height);
        const std::uint32_t y1 = std::max(y0 + 1, static_cast<std::uint32_t>(static_cast<std::uint64_t>(y + 1) * image.height / out.height));
        for (std::uint32_t x = 0; x < out.width; ++x) {
            const std::uint32_t x0 = static_cast<std::uint32_t>(static_cast<std::uint64_t>(x) * image.width / out.width);
            const std::uint32_t x1 = std::max(x0 + 1, static_cast<std::uint32_t>(static_cast<std::uint64_t>(x + 1) * image.width / out.width));
            std::uint64_t sums[4] = {0, 0, 0, 0};
            std::uint64_t count = 0;
            for (std::uint32_t sy = y0; sy < y1 && sy < image.height; ++sy) {
                for (std::uint32_t sx = x0; sx < x1 && sx < image.width; ++sx) {
                    const std::uint8_t* p = image.pixels.data() + (static_cast<std::size_t>(sy) * image.width + sx) * 4;
                    for (int c = 0; c < 4; ++c) {
                        sums[c] += p[c];
                    }
                    count += 1;
                }
            }
            std::uint8_t* o = out.pixels.data() + (static_cast<std::size_t>(y) * out.width + x) * 4;
            for (int c = 0; c < 4; ++c) {
                o[c] = static_cast<std::uint8_t>(count ? sums[c] / count : 0);
            }
        }
    }
    return out;
}

Bytes encodePng(const Rgba& image) {
    Bytes png(std::begin(kPngSignature), std::end(kPngSignature));
    Bytes header;
    appendU32BE(header, image.width);
    appendU32BE(header, image.height);
    header.push_back(8);  // bit depth
    header.push_back(6);  // RGBA
    header.push_back(0);  // compression
    header.push_back(0);  // filter
    header.push_back(0);  // no interlacing
    appendChunk(png, "IHDR", header);

    // The pixels, each row starting with filter type 0. They go in zlib stored blocks, which need no compression.
    const std::size_t stride = static_cast<std::size_t>(image.width) * 4;
    Bytes raw;
    raw.reserve((stride + 1) * image.height);
    for (std::uint32_t y = 0; y < image.height; ++y) {
        raw.push_back(0);
        const std::uint8_t* row = image.pixels.data() + y * stride;
        raw.insert(raw.end(), row, row + stride);
    }
    Bytes zlib{0x78, 0x01};
    std::size_t at = 0;
    do {
        const std::size_t length = std::min<std::size_t>(65535, raw.size() - at);
        const bool last = at + length == raw.size();
        zlib.push_back(last ? 1 : 0);
        zlib.push_back(static_cast<std::uint8_t>(length));
        zlib.push_back(static_cast<std::uint8_t>(length >> 8));
        zlib.push_back(static_cast<std::uint8_t>(~length));
        zlib.push_back(static_cast<std::uint8_t>(~length >> 8));
        zlib.insert(zlib.end(), raw.begin() + static_cast<std::ptrdiff_t>(at), raw.begin() + static_cast<std::ptrdiff_t>(at + length));
        at += length;
    } while (at < raw.size());
    appendU32BE(zlib, adler32(raw));
    appendChunk(png, "IDAT", zlib);
    appendChunk(png, "IEND", Bytes{});
    return png;
}

}  // namespace gwl
