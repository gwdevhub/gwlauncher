execute_process(COMMAND "${READELF}" -d "${EXECUTABLE}"
    RESULT_VARIABLE result OUTPUT_VARIABLE dependencies ERROR_VARIABLE error)
if(NOT result EQUAL 0)
    message(FATAL_ERROR "Could not inspect ${EXECUTABLE}: ${error}")
endif()

if(dependencies MATCHES "NEEDED[^\n]*\\[lib(crypto|ssl|z|zstd)\\.so")
    message(FATAL_ERROR "Crypto dependencies must be linked statically:\n${dependencies}")
endif()
message(STATUS "No dynamic libcrypto, libssl, zlib or zstd dependency in ${EXECUTABLE}")
