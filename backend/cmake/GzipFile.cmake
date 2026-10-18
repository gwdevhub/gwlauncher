# Writes INPUT gzip-compressed to OUTPUT. The page is stored this way in the executable, and the launcher decompresses it at startup.
execute_process(COMMAND gzip -9 -n -c "${INPUT}" OUTPUT_FILE "${OUTPUT}" RESULT_VARIABLE result)
if(NOT result EQUAL 0)
    message(FATAL_ERROR "gzip couldn't compress ${INPUT}: the gzip tool is needed to build the launcher.")
endif()
