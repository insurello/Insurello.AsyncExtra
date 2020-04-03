PROJECT_NAME=AsyncExtra
TEST_PROJECT_NAME=$(PROJECT_NAME).Tests
PROJECT_DIR=src/$(PROJECT_NAME)
TEST_DIR=src/$(TEST_PROJECT_NAME)
MAIN_PROJ=$(MAIN_DIR)/$(PROJECT_NAME).fsproj
TEST_PROJ=$(TEST_DIR)/$(TEST_PROJECT_NAME).fsproj


.PHONY : all build test

all: build test

test:
	dotnet test $(TEST_PROJ)

test-watch:
	dotnet watch --project $(TEST_DIR) run

clean:
	rm -r $(PROJECT_DIR)/bin $(PROJECT_DIR)/obj $(TEST_DIR)/bin $(TEST_DIR)/obj
