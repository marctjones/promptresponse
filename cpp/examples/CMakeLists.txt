add_executable(create_template create_template.cpp)
target_link_libraries(create_template promptresponse)

add_executable(fill_form fill_form.cpp)
target_link_libraries(fill_form promptresponse)

if(WITH_SIGNATURES)
    add_executable(sign_verify sign_verify.cpp)
    target_link_libraries(sign_verify promptresponse)
endif()
